using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Domain.Rules;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class ChuyenHopDongGiuChoStore(ApplicationDbContext context) : IChuyenHopDongGiuChoStore
{
    public async Task<ContractCandidateDto?> GetCandidateAsync(int reservationId, int actorId)
    {
        var role = await StaffRoleAsync(actorId);
        if (role is null) return null;
        var hold = await AuthorizedHolds(actorId, role.Value).AsNoTracking()
            .Include(item => item.PhongTro).ThenInclude(item => item.ChiNhanh)
            .Include(item => item.KhachVangLai)
            .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == reservationId &&
                item.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho &&
                item.QuyetDinhHoanTienGiuCho == null);
        if (hold is null) return null;
        var terms = await context.DieuKhoanMaus.AsNoTracking().Where(item => !item.IsDeleted)
            .OrderBy(item => item.DieuKhoanMauId)
            .Select(item => new ContractTermDto(item.DieuKhoanMauId, item.TieuDe, item.NoiDung)).ToListAsync();
        return new(hold.YeuCauGiuChoId, hold.PhongTro.SoPhong, hold.PhongTro.ChiNhanh.TenChiNhanh,
            hold.KhachVangLai.HoTen, hold.KhachVangLai.Email, hold.KhachVangLai.CCCD,
            hold.GiaThueDaChot ?? hold.PhongTro.GiaThue, hold.SoTienGiuCho ?? 0,
            hold.HanKyHopDong, terms);
    }

    public async Task<int?> ConvertAsync(int reservationId, ReservationContractRequest request, int actorId)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var role = await StaffRoleAsync(actorId);
            if (role is null) return null;
            var roomId = await AuthorizedHolds(actorId, role.Value)
                .Where(item => item.YeuCauGiuChoId == reservationId)
                .Select(item => (int?)item.PhongTroId).FirstOrDefaultAsync();
            if (roomId is null) return null;
            var room = await context.PhongTros.FromSqlInterpolated(
                $"SELECT * FROM phong_tro WHERE \"PhongTroId\" = {roomId.Value} FOR UPDATE")
                .SingleOrDefaultAsync();
            var hold = await AuthorizedHolds(actorId, role.Value)
                .Include(item => item.KhachVangLai).ThenInclude(item => item.NguoiDung)
                .Include(item => item.ApDungTienGiuChoVaoTienCoc)
                .Include(item => item.QuyetDinhHoanTienGiuCho)
                .Include(item => item.YeuCauThanhToanGiuChos).ThenInclude(item => item.GiaoDichGiuChos)
                .FirstOrDefaultAsync(item => item.YeuCauGiuChoId == reservationId);
            if (hold is null || room is null || room.IsDeleted) return null;
            if (hold.TrangThai == TrangThaiYeuCauGiuCho.DaChuyenHopDong &&
                hold.ApDungTienGiuChoVaoTienCoc is not null)
                return hold.ApDungTienGiuChoVaoTienCoc.HopDongId;
            var guest = hold.KhachVangLai;
            var existingTenant = guest.NguoiDung.Role == Role.KhachThue &&
                guest.NguoiDung.NguoiThueId is int tenantId
                ? await context.NguoiThues.FirstOrDefaultAsync(item =>
                    item.NguoiThueId == tenantId && !item.IsDeleted)
                : null;
            if (hold.TrangThai != TrangThaiYeuCauGiuCho.DangGiuCho ||
                hold.ApDungTienGiuChoVaoTienCoc is not null || hold.QuyetDinhHoanTienGiuCho is not null ||
                room.TrangThai != TrangThaiPhong.Trong || room.SoNguoiToiDa < 1 ||
                guest.NguoiDung.Role is not (Role.KhachVangLai or Role.KhachThue) ||
                !guest.NguoiDung.IsActive || guest.NguoiDung.IsDeleted ||
                (guest.NguoiDung.Role == Role.KhachVangLai && guest.NguoiDung.NguoiThueId is not null) ||
                (guest.NguoiDung.Role == Role.KhachThue && existingTenant is null) ||
                string.IsNullOrWhiteSpace(guest.Email) || string.IsNullOrWhiteSpace(guest.CCCD) ||
                (hold.HanKyHopDong < DateTime.UtcNow && !request.XacNhanKyQuaHan)) return null;

            var rent = hold.GiaThueDaChot ?? room.GiaThue;
            var deposit = hold.SoTienGiuCho.GetValueOrDefault();
            var payments = hold.YeuCauThanhToanGiuChos.SelectMany(item => item.GiaoDichGiuChos).ToList();
            if (rent <= 0 || deposit != GiuChoRules.TinhTienGiuCho(rent) ||
                payments.Count != 1 || payments[0].SoTienThucNhan != deposit ||
                await context.HopDongs.AnyAsync(item => item.PhongTroId == room.PhongTroId &&
                    !item.IsDeleted && item.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong) ||
                await context.NguoiThues.AnyAsync(item => item.CCCD == guest.CCCD &&
                    item.NguoiThueId != (existingTenant == null ? 0 : existingTenant.NguoiThueId))) return null;

            var selectedTerms = await context.DieuKhoanMaus.Where(item => !item.IsDeleted &&
                request.DieuKhoanMauIds.Contains(item.DieuKhoanMauId)).OrderBy(item => item.DieuKhoanMauId).ToListAsync();
            if (selectedTerms.Count != request.DieuKhoanMauIds.Count) return null;
            var now = DateTime.UtcNow;
            var tenant = existingTenant ?? new NguoiThue
            {
                HoVaTen = guest.HoTen, Email = guest.Email, SoDienThoai = guest.SoDienThoai,
                CCCD = guest.CCCD, NgayTao = now
            };
            var contract = new HopDong
            {
                MaHopDong = $"HD-GC-{reservationId}", PhongTroId = room.PhongTroId,
                NguoiThue = tenant, ThoiDiemBatDau = request.NgayBatDau.UtcDateTime,
                ThoiDiemKetThuc = request.NgayKetThuc?.UtcDateTime,
                TienThuePhong = rent, TienCocPhong = deposit,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong, NgayTao = now,
                ChiTietThanhVienHopDongs = [new ChiTietThanhVienHopDong
                { NguoiThue = tenant, NgayVao = request.NgayBatDau.UtcDateTime }],
                HopDongDieuKhoans = selectedTerms.Select((term, index) => new HopDongDieuKhoan
                { TieuDe = term.TieuDe, NoiDung = term.NoiDung, ThuTu = index + 1 }).ToList()
            };
            context.HopDongs.Add(contract);
            context.ApDungTienGiuChoVaoTienCocs.Add(new ApDungTienGiuChoVaoTienCoc
            {
                YeuCauGiuChoId = reservationId, HopDong = contract, SoTienApDung = deposit,
                NguoiThucHienId = actorId, NgayApDung = now,
                GhiChu = "Cấn trừ vào hóa đơn tháng bắt đầu hợp đồng; dư chuyển các tháng sau."
            });
            if (existingTenant is null)
            {
                guest.NguoiDung.NguoiThue = tenant;
                guest.NguoiDung.Role = Role.KhachThue;
            }
            room.TrangThai = TrangThaiPhong.DaThue;
            room.NgayCapNhat = now;
            hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                TrangThaiTruoc = hold.TrangThai, TrangThaiSau = TrangThaiYeuCauGiuCho.DaChuyenHopDong,
                LoaiTacNhan = LoaiTacNhan.NguoiDung, NguoiThucHienId = actorId,
                NgayThucHien = now, LyDo = request.XacNhanKyQuaHan ? "Nhân viên xác nhận ký quá hạn." : null
            });
            hold.TrangThai = TrangThaiYeuCauGiuCho.DaChuyenHopDong;
            hold.NgayCapNhat = now;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return contract.HopDongId;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex)) { return null; }
    }

    private async Task<Role?> StaffRoleAsync(int actorId) => await context.NguoiDungs.AsNoTracking()
        .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
            (user.Role == Role.Admin || user.Role == Role.NhanVien))
        .Select(user => (Role?)user.Role).FirstOrDefaultAsync();

    private IQueryable<YeuCauGiuCho> AuthorizedHolds(int actorId, Role role) => context.YeuCauGiuChos
        .Where(hold => role == Role.Admin || context.NhanVienChiNhanhs.Any(assignment =>
            assignment.NguoiDungId == actorId && assignment.IsActive &&
            assignment.NgayThuHoi == null && assignment.ChiNhanhId == hold.PhongTro.ChiNhanhId));
}
