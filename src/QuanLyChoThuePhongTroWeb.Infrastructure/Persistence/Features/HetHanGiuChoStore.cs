using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class HetHanGiuChoStore(ApplicationDbContext context) : IHetHanGiuChoStore
{
    public async Task<IReadOnlyList<int>> GetCandidatesAsync(DateTime nowUtc) =>
        await context.YeuCauGiuChos.AsNoTracking()
            .Where(hold => hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
                hold.HanThanhToan != null && hold.HanThanhToan.Value.AddMinutes(5) <= nowUtc &&
                !hold.YeuCauThanhToanGiuChos.Any(payment => payment.MinhChungThanhToanGiuChos.Any()))
            .OrderBy(hold => hold.HanThanhToan)
            .Select(hold => hold.YeuCauGiuChoId)
            .Take(100)
            .ToListAsync();

    public async Task<bool> TryExpireAsync(int id, DateTime nowUtc)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hold = await context.YeuCauGiuChos
                .Include(item => item.YeuCauThanhToanGiuChos)
                    .ThenInclude(item => item.MinhChungThanhToanGiuChos)
                .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == id);
            if (hold is null || hold.TrangThai != TrangThaiYeuCauGiuCho.ChoThanhToan ||
                hold.HanThanhToan is null || hold.HanThanhToan.Value.AddMinutes(5) > nowUtc ||
                hold.YeuCauThanhToanGiuChos.Any(payment => payment.MinhChungThanhToanGiuChos.Count > 0))
                return false;

            hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                TrangThaiTruoc = hold.TrangThai,
                TrangThaiSau = TrangThaiYeuCauGiuCho.HetHan,
                LoaiTacNhan = LoaiTacNhan.HeThong,
                NgayThucHien = nowUtc,
                LyDo = "Quá hạn chuyển tiền 5 phút mà chưa gửi minh chứng."
            });
            hold.TrangThai = TrangThaiYeuCauGiuCho.HetHan;
            hold.NgayCapNhat = nowUtc;
            foreach (var payment in hold.YeuCauThanhToanGiuChos)
            {
                payment.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauThanhToanGiuCho
                {
                    TrangThaiTruoc = payment.TrangThai,
                    TrangThaiSau = TrangThaiYeuCauThanhToan.HetHan,
                    LoaiTacNhan = LoaiTacNhan.HeThong,
                    NgayThucHien = nowUtc,
                    LyDo = "Yêu cầu giữ chỗ hết hạn."
                });
                payment.TrangThai = TrangThaiYeuCauThanhToan.HetHan;
                payment.NgayCapNhat = nowUtc;
            }
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex))
        {
            // A rolled back batch item must not be saved by the next item in this scope.
            context.ChangeTracker.Clear();
            return false;
        }
    }
}
