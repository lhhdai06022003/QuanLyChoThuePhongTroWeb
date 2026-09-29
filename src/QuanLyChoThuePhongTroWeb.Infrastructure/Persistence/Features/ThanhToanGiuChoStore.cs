using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class ThanhToanGiuChoStore(ApplicationDbContext context) : IThanhToanGiuChoStore
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    public async Task<HoldPaymentDto?> GetForGuestAsync(int reservationId, int actorId) =>
        await context.YeuCauThanhToanGiuChos.AsNoTracking()
            .Where(payment => payment.YeuCauGiuChoId == reservationId &&
                payment.YeuCauGiuCho.KhachVangLai.NguoiDungId == actorId &&
                payment.YeuCauGiuCho.KhachVangLai.NguoiDung.IsActive &&
                !payment.YeuCauGiuCho.KhachVangLai.NguoiDung.IsDeleted)
            .OrderByDescending(payment => payment.NgayTao)
            .Select(payment => new HoldPaymentDto(payment.YeuCauThanhToanGiuChoId,
                payment.YeuCauGiuChoId, payment.YeuCauGiuCho.PhongTro.SoPhong,
                payment.SoTien, payment.HanThanhToan, payment.MaYeuCau,
                payment.NoiDungChuyenKhoan, payment.TrangThai.ToString(),
                payment.MinhChungThanhToanGiuChos.OrderByDescending(proof => proof.NgayTao)
                    .Select(proof => (int?)proof.MinhChungThanhToanGiuChoId).FirstOrDefault(),
                payment.MinhChungThanhToanGiuChos.OrderByDescending(proof => proof.NgayTao)
                    .Select(proof => proof.TrangThai.ToString()).FirstOrDefault()))
            .FirstOrDefaultAsync();

    public Task<bool> CanSubmitAsync(int paymentId, int actorId, DateTime nowUtc) =>
        context.YeuCauThanhToanGiuChos.AsNoTracking().AnyAsync(payment =>
            payment.YeuCauThanhToanGiuChoId == paymentId &&
            payment.YeuCauGiuCho.KhachVangLai.NguoiDungId == actorId &&
            payment.YeuCauGiuCho.KhachVangLai.NguoiDung.IsActive &&
            !payment.YeuCauGiuCho.KhachVangLai.NguoiDung.IsDeleted &&
            payment.YeuCauGiuCho.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
            payment.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan &&
            nowUtc <= payment.HanThanhToan.AddMinutes(5) &&
            !payment.MinhChungThanhToanGiuChos.Any());

    public async Task<bool> TrySubmitAsync(int paymentId, int actorId, string key,
        SubmitProofRequest request, DateTime nowUtc)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var payment = await context.YeuCauThanhToanGiuChos
                .Include(item => item.YeuCauGiuCho)
                .Include(item => item.MinhChungThanhToanGiuChos)
                .FirstOrDefaultAsync(item => item.YeuCauThanhToanGiuChoId == paymentId &&
                    item.YeuCauGiuCho.KhachVangLai.NguoiDungId == actorId &&
                    item.YeuCauGiuCho.KhachVangLai.NguoiDung.IsActive &&
                    !item.YeuCauGiuCho.KhachVangLai.NguoiDung.IsDeleted);
            if (payment is null || payment.YeuCauGiuCho.TrangThai != TrangThaiYeuCauGiuCho.ChoThanhToan ||
                payment.TrangThai != TrangThaiYeuCauThanhToan.ChoThanhToan ||
                nowUtc > payment.HanThanhToan.Add(Grace) || payment.MinhChungThanhToanGiuChos.Count > 0)
                return false;

            payment.MinhChungThanhToanGiuChos.Add(new MinhChungThanhToanGiuCho
            {
                UrlHinhAnh = key,
                PublicIdHinhAnh = key,
                SoTienKhaiBao = request.SoTienKhaiBao,
                NgayChuyenTien = request.NgayChuyenTien.UtcDateTime,
                MaGiaoDichNganHang = request.MaGiaoDichNganHang?.Trim(),
                GhiChuKhachHang = request.GhiChu?.Trim(),
                TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan,
                NgayTao = nowUtc
            });
            MovePayment(payment, TrangThaiYeuCauThanhToan.DaBaoChuyen, LoaiTacNhan.NguoiDung, actorId, nowUtc);
            MoveHold(payment.YeuCauGiuCho, TrangThaiYeuCauGiuCho.ChoXacNhanTien,
                LoaiTacNhan.NguoiDung, actorId, nowUtc);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    public async Task<IReadOnlyList<ProofReviewDto>> GetReviewQueueAsync(int actorId)
    {
        var role = await GetStaffRoleAsync(actorId);
        if (role is null) return Array.Empty<ProofReviewDto>();
        return await context.MinhChungThanhToanGiuChos.AsNoTracking()
            .Where(proof => role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                assignment.NguoiDungId == actorId && assignment.IsActive &&
                assignment.NgayThuHoi == null && assignment.ChiNhanhId ==
                    proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.PhongTro.ChiNhanhId))
            .OrderByDescending(proof => proof.NgayTao)
            .Select(proof => new ProofReviewDto(proof.MinhChungThanhToanGiuChoId,
                proof.YeuCauThanhToanGiuChoId, proof.YeuCauThanhToanGiuCho.YeuCauGiuChoId,
                proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.PhongTro.SoPhong,
                proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.PhongTro.ChiNhanh.TenChiNhanh,
                proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.KhachVangLai.HoTen,
                proof.YeuCauThanhToanGiuCho.SoTien, proof.SoTienKhaiBao,
                proof.NgayChuyenTien, proof.MaGiaoDichNganHang, proof.TrangThai.ToString()))
            .ToListAsync();
    }

    public async Task<string?> GetAuthorizedProofKeyAsync(int proofId, int actorId)
    {
        var user = await context.NguoiDungs.AsNoTracking()
            .Where(item => item.NguoiDungId == actorId && item.IsActive && !item.IsDeleted)
            .Select(item => new { item.Role }).FirstOrDefaultAsync();
        if (user is null) return null;
        return await context.MinhChungThanhToanGiuChos.AsNoTracking()
            .Where(proof => proof.MinhChungThanhToanGiuChoId == proofId &&
                (proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.KhachVangLai.NguoiDungId == actorId ||
                 user.Role == Role.Admin ||
                 (user.Role == Role.NhanVien && context.NhanVienChiNhanhs.Any(assignment =>
                     assignment.NguoiDungId == actorId && assignment.IsActive &&
                     assignment.NgayThuHoi == null && assignment.ChiNhanhId ==
                         proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.PhongTro.ChiNhanhId))))
            .Select(proof => proof.UrlHinhAnh).FirstOrDefaultAsync();
    }

    public async Task<bool> ConfirmAsync(int proofId, int actorId, string bankReference,
        decimal actualAmount, DateTime receivedUtc)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var proof = await LoadReviewableProofAsync(proofId, actorId);
            if (proof is null) return false;
            var payment = proof.YeuCauThanhToanGiuCho;
            if (actualAmount != payment.SoTien || receivedUtc > payment.HanThanhToan.Add(Grace) ||
                receivedUtc < payment.NgayTao.AddMinutes(-5)) return false;
            if (await context.GiaoDichGiuChos.AnyAsync(item => item.MaGiaoDich == bankReference)) return false;

            context.GiaoDichGiuChos.Add(new GiaoDichGiuCho
            {
                YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                MinhChungThanhToanGiuChoId = proofId,
                MaGiaoDich = bankReference,
                SoTienThucNhan = actualAmount,
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NgayThucNhan = receivedUtc,
                NguoiXacNhanId = actorId,
                NgayXacNhan = DateTime.UtcNow
            });
            proof.TrangThai = TrangThaiMinhChungThanhToan.DaXacNhan;
            proof.NguoiDoiChieuId = actorId;
            proof.NgayDoiChieu = DateTime.UtcNow;
            MovePayment(payment, TrangThaiYeuCauThanhToan.DaHoanTat,
                LoaiTacNhan.NguoiDung, actorId, DateTime.UtcNow);
            MoveHold(payment.YeuCauGiuCho, TrangThaiYeuCauGiuCho.DangGiuCho,
                LoaiTacNhan.NguoiDung, actorId, DateTime.UtcNow);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    public async Task<bool> RejectAsync(int proofId, int actorId, string reason,
        decimal actualAmount, string? bankReference, DateTime? receivedUtc)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var proof = await LoadReviewableProofAsync(proofId, actorId);
            if (proof is null) return false;
            var payment = proof.YeuCauThanhToanGiuCho;
            if (actualAmount > 0)
            {
                if (bankReference is null || receivedUtc is null ||
                    await context.GiaoDichGiuChos.AnyAsync(item => item.MaGiaoDich == bankReference)) return false;
                context.GiaoDichGiuChos.Add(new GiaoDichGiuCho
                {
                    YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                    MinhChungThanhToanGiuChoId = proofId,
                    MaGiaoDich = bankReference,
                    SoTienThucNhan = actualAmount,
                    PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                    NgayThucNhan = receivedUtc.Value,
                    NguoiXacNhanId = actorId,
                    NgayXacNhan = DateTime.UtcNow,
                    GhiChu = ("Tiền thực nhận khi từ chối giữ chỗ: " + reason)[..Math.Min(500,
                        "Tiền thực nhận khi từ chối giữ chỗ: ".Length + reason.Length)]
                });
            }
            proof.TrangThai = TrangThaiMinhChungThanhToan.TuChoi;
            proof.LyDoTuChoi = reason;
            proof.NguoiDoiChieuId = actorId;
            proof.NgayDoiChieu = DateTime.UtcNow;
            MovePayment(payment, TrangThaiYeuCauThanhToan.TuChoi,
                LoaiTacNhan.NguoiDung, actorId, DateTime.UtcNow, reason);
            payment.YeuCauGiuCho.LyDoTuChoiHoacHuy = reason;
            MoveHold(payment.YeuCauGiuCho, TrangThaiYeuCauGiuCho.TuChoi,
                LoaiTacNhan.NguoiDung, actorId, DateTime.UtcNow, reason);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    private async Task<Role?> GetStaffRoleAsync(int actorId) =>
        await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
                (user.Role == Role.Admin || user.Role == Role.NhanVien))
            .Select(user => (Role?)user.Role).FirstOrDefaultAsync();

    private async Task<MinhChungThanhToanGiuCho?> LoadReviewableProofAsync(int proofId, int actorId)
    {
        var role = await GetStaffRoleAsync(actorId);
        if (role is null) return null;
        return await context.MinhChungThanhToanGiuChos
            .Include(item => item.YeuCauThanhToanGiuCho).ThenInclude(item => item.YeuCauGiuCho)
            .FirstOrDefaultAsync(proof => proof.MinhChungThanhToanGiuChoId == proofId &&
                proof.TrangThai == TrangThaiMinhChungThanhToan.ChoXacNhan &&
                proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien &&
                (role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                    assignment.NguoiDungId == actorId && assignment.IsActive &&
                    assignment.NgayThuHoi == null && assignment.ChiNhanhId ==
                        proof.YeuCauThanhToanGiuCho.YeuCauGiuCho.PhongTro.ChiNhanhId)));
    }

    private static void MovePayment(YeuCauThanhToanGiuCho payment, TrangThaiYeuCauThanhToan next,
        LoaiTacNhan actorType, int actorId, DateTime now, string? reason = null)
    {
        payment.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauThanhToanGiuCho
        {
            TrangThaiTruoc = payment.TrangThai,
            TrangThaiSau = next,
            LoaiTacNhan = actorType,
            NguoiThucHienId = actorId,
            NgayThucHien = now,
            LyDo = reason
        });
        payment.TrangThai = next;
        payment.NgayCapNhat = now;
    }

    private static void MoveHold(YeuCauGiuCho hold, TrangThaiYeuCauGiuCho next,
        LoaiTacNhan actorType, int actorId, DateTime now, string? reason = null)
    {
        hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
        {
            TrangThaiTruoc = hold.TrangThai,
            TrangThaiSau = next,
            LoaiTacNhan = actorType,
            NguoiThucHienId = actorId,
            NgayThucHien = now,
            LyDo = reason
        });
        hold.TrangThai = next;
        hold.NgayCapNhat = now;
    }
}
