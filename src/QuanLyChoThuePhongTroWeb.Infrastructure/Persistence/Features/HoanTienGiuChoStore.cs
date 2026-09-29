using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class HoanTienGiuChoStore(ApplicationDbContext context) : IHoanTienGiuChoStore
{
    public async Task<IReadOnlyList<RefundCandidateDto>> GetQueueAsync(int actorId)
    {
        var role = await StaffRoleAsync(actorId);
        if (role is null) return [];
        return await Project(context.YeuCauGiuChos.AsNoTracking()
            .Where(hold => (hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho ||
                    hold.TrangThai == TrangThaiYeuCauGiuCho.TuChoi ||
                    hold.TrangThai == TrangThaiYeuCauGiuCho.HetHan ||
                    hold.TrangThai == TrangThaiYeuCauGiuCho.DaHuy) &&
                (role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
                    assignment.NguoiDungId == actorId && assignment.IsActive &&
                    assignment.NgayThuHoi == null && assignment.ChiNhanhId == hold.PhongTro.ChiNhanhId)))
            .OrderByDescending(hold => hold.NgayTao)).ToListAsync();
    }

    public async Task<IReadOnlyList<RefundCandidateDto>> GetMineAsync(int actorId) =>
        await Project(context.YeuCauGiuChos.AsNoTracking()
            .Where(hold => hold.KhachVangLai.NguoiDungId == actorId &&
                hold.KhachVangLai.NguoiDung.IsActive &&
                !hold.KhachVangLai.NguoiDung.IsDeleted &&
                hold.QuyetDinhHoanTienGiuCho != null)
            .OrderByDescending(hold => hold.NgayTao)).ToListAsync();

    public async Task<bool> DecideAsync(int reservationId, RefundDecisionRequest request, int actorId)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var role = await StaffRoleAsync(actorId);
            if (role is null) return false;
            var hold = await context.YeuCauGiuChos
                .Include(item => item.YeuCauThanhToanGiuChos).ThenInclude(item => item.GiaoDichGiuChos)
                .Include(item => item.QuyetDinhHoanTienGiuCho)
                .Include(item => item.ApDungTienGiuChoVaoTienCoc)
                .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == reservationId);
            if (hold is null || hold.QuyetDinhHoanTienGiuCho is not null ||
                hold.ApDungTienGiuChoVaoTienCoc is not null ||
                hold.TrangThai is not (TrangThaiYeuCauGiuCho.DangGiuCho or
                    TrangThaiYeuCauGiuCho.TuChoi or TrangThaiYeuCauGiuCho.HetHan or
                    TrangThaiYeuCauGiuCho.DaHuy) ||
                !await CanManageBranchAsync(actorId, role.Value, hold.PhongTroId)) return false;

            var received = hold.YeuCauThanhToanGiuChos
                .SelectMany(item => item.GiaoDichGiuChos).Sum(item => item.SoTienThucNhan);
            if (request.SoTienHoanDuyet > received) return false;
            var now = DateTime.UtcNow;
            if (hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)
            {
                hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
                {
                    TrangThaiTruoc = hold.TrangThai,
                    TrangThaiSau = TrangThaiYeuCauGiuCho.DaHuy,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = now,
                    LyDo = request.LyDo
                });
                hold.TrangThai = TrangThaiYeuCauGiuCho.DaHuy;
                hold.LyDoTuChoiHoacHuy = request.LyDo;
            }
            hold.NgayCapNhat = now;
            context.QuyetDinhHoanTienGiuChos.Add(new QuyetDinhHoanTienGiuCho
            {
                YeuCauGiuChoId = reservationId,
                SoTienHoanDuyet = request.SoTienHoanDuyet,
                LyDo = request.LyDo,
                NguoiQuyetDinhId = actorId,
                NgayQuyetDinh = now,
                TrangThai = request.SoTienHoanDuyet == 0
                    ? TrangThaiHoanTien.KhongHoan : TrangThaiHoanTien.ChoHoanTien
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    public async Task<bool> RecordAsync(int decisionId, RecordRefundRequest request, int actorId)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var role = await StaffRoleAsync(actorId);
            if (role is null) return false;
            var decision = await context.QuyetDinhHoanTienGiuChos
                .Include(item => item.YeuCauGiuCho).ThenInclude(item => item.YeuCauThanhToanGiuChos)
                    .ThenInclude(item => item.GiaoDichGiuChos)
                .Include(item => item.YeuCauGiuCho).ThenInclude(item => item.ApDungTienGiuChoVaoTienCoc)
                .Include(item => item.GiaoDichHoanTiens)
                .FirstOrDefaultAsync(item => item.QuyetDinhHoanTienGiuChoId == decisionId);
            if (decision is null || decision.TrangThai is TrangThaiHoanTien.DaHuy or
                    TrangThaiHoanTien.KhongHoan or TrangThaiHoanTien.DaHoanTien ||
                !await CanManageBranchAsync(actorId, role.Value, decision.YeuCauGiuCho.PhongTroId) ||
                await context.GiaoDichHoanTienGiuChos.AnyAsync(item =>
                    item.MaGiaoDichHoan == request.MaGiaoDichHoan)) return false;

            var refunded = decision.GiaoDichHoanTiens.Sum(item => item.SoTienHoan);
            var received = decision.YeuCauGiuCho.YeuCauThanhToanGiuChos
                .SelectMany(item => item.GiaoDichGiuChos).Sum(item => item.SoTienThucNhan);
            var applied = decision.YeuCauGiuCho.ApDungTienGiuChoVaoTienCoc?.SoTienApDung ?? 0;
            if (refunded + request.SoTienHoan > decision.SoTienHoanDuyet ||
                refunded + request.SoTienHoan + applied > received) return false;

            context.GiaoDichHoanTienGiuChos.Add(new GiaoDichHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = decisionId,
                SoTienHoan = request.SoTienHoan,
                MaGiaoDichHoan = request.MaGiaoDichHoan,
                NguoiXacNhanId = actorId,
                NgayHoan = request.NgayHoan.UtcDateTime,
                GhiChu = request.GhiChu
            });
            decision.TrangThai = refunded + request.SoTienHoan == decision.SoTienHoanDuyet
                ? TrangThaiHoanTien.DaHoanTien : TrangThaiHoanTien.DangHoanTien;
            decision.YeuCauGiuCho.NgayCapNhat = DateTime.UtcNow;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return false; }
    }

    private async Task<Role?> StaffRoleAsync(int actorId) =>
        await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
                (user.Role == Role.Admin || user.Role == Role.NhanVien))
            .Select(user => (Role?)user.Role).FirstOrDefaultAsync();

    private Task<bool> CanManageBranchAsync(int actorId, Role role, int roomId) =>
        role == Role.Admin ? Task.FromResult(true) :
            context.PhongTros.AsNoTracking().AnyAsync(room => room.PhongTroId == roomId &&
                context.NhanVienChiNhanhs.Any(assignment =>
                    assignment.NguoiDungId == actorId && assignment.IsActive &&
                    assignment.NgayThuHoi == null && assignment.ChiNhanhId == room.ChiNhanhId));

    private static IQueryable<RefundCandidateDto> Project(IQueryable<YeuCauGiuCho> holds) =>
        holds.Select(hold => new RefundCandidateDto(hold.YeuCauGiuChoId,
            hold.PhongTro.SoPhong, hold.PhongTro.ChiNhanh.TenChiNhanh,
            hold.KhachVangLai.HoTen, hold.TrangThai.ToString(),
            hold.YeuCauThanhToanGiuChos.SelectMany(item => item.GiaoDichGiuChos)
                .Sum(item => item.SoTienThucNhan),
            hold.QuyetDinhHoanTienGiuCho == null ? null :
                hold.QuyetDinhHoanTienGiuCho.SoTienHoanDuyet,
            hold.QuyetDinhHoanTienGiuCho == null ? 0 :
                hold.QuyetDinhHoanTienGiuCho.GiaoDichHoanTiens.Sum(item => item.SoTienHoan),
            hold.QuyetDinhHoanTienGiuCho == null ? null :
                hold.QuyetDinhHoanTienGiuCho.TrangThai.ToString(),
            hold.QuyetDinhHoanTienGiuCho == null ? null :
                hold.QuyetDinhHoanTienGiuCho.LyDo,
            hold.QuyetDinhHoanTienGiuCho == null ? null :
                hold.QuyetDinhHoanTienGiuCho.QuyetDinhHoanTienGiuChoId));
}
