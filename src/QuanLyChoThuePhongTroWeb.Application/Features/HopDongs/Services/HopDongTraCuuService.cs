using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public class HopDongTraCuuService : IHopDongTraCuuService
    {
        private const string KhongTimThayNguoiThue = "Không tìm thấy người thuê.";

        private readonly IHopDongLifecycleStore _store;
        private readonly IEmployeeAccessService _access;
        private readonly ITenantVisibilityStore _tenantVisibility;
        private readonly TimeProvider _timeProvider;

        public HopDongTraCuuService(
            IHopDongLifecycleStore store,
            IEmployeeAccessService access,
            ITenantVisibilityStore tenantVisibility,
            TimeProvider timeProvider)
        {
            _store = store;
            _access = access;
            _tenantVisibility = tenantVisibility;
            _timeProvider = timeProvider;
        }

        public async Task<ServiceResult<HopDongChiTietDto>> GetChiTietAsync(int actorId, int hopDongId, CancellationToken ct = default)
        {
            var hopDong = await _store.GetHopDongAsync(hopDongId, ct);
            if (hopDong == null)
            {
                return ServiceResult<HopDongChiTietDto>.NotFound(HopDongAccessGuard.KhongTimThayHopDong);
            }

            var denied = await HopDongAccessGuard.CheckAsync(_access, actorId, hopDong.PhongTro.ChiNhanhId, null, ct);
            if (denied != null)
            {
                return HopDongAccessGuard.As<HopDongChiTietDto>(denied);
            }

            var goc = HopDongCodeChain.RootOf(hopDong.MaHopDong);
            var chain = (await _store.GetChainAsync(goc, ct))
                .Where(x => HopDongCodeChain.BelongsTo(x.MaHopDong, goc))
                .OrderBy(x => HopDongCodeChain.RenewalIndex(x.MaHopDong))
                .ToList();
            if (chain.All(x => x.HopDongId != hopDong.HopDongId))
            {
                chain.Add(hopDong);
                chain = chain.OrderBy(x => HopDongCodeChain.RenewalIndex(x.MaHopDong)).ToList();
            }

            var keTiep = TimKeTiep(chain, hopDong);
            var members = await _store.GetMembersAsync(hopDongId, ct);
            var dieuKhoan = await _store.GetTermsAsync(hopDongId, ct);

            return ServiceResult<HopDongChiTietDto>.Ok(new HopDongChiTietDto
            {
                HopDongId = hopDong.HopDongId,
                MaHopDong = hopDong.MaHopDong,
                MaGoc = goc,
                LoaiText = HopDongCodeChain.LoaiText(hopDong.MaHopDong),
                PhongTroId = hopDong.PhongTroId,
                SoPhong = hopDong.PhongTro.SoPhong,
                ChiNhanhId = hopDong.PhongTro.ChiNhanhId,
                TenChiNhanh = hopDong.PhongTro.ChiNhanh.TenChiNhanh,
                NguoiThueId = hopDong.NguoiThueId,
                TenNguoiDaiDien = hopDong.NguoiThue.HoVaTen,
                ThoiDiemBatDauUtc = hopDong.ThoiDiemBatDau,
                ThoiDiemKetThucUtc = hopDong.ThoiDiemKetThuc,
                TienThue = hopDong.TienThuePhong,
                TienCoc = hopDong.TienCocPhong,
                TrangThai = (int)hopDong.TrangThaiHopDong,
                TrangThaiText = TrangThaiText(hopDong, keTiep),
                HopDongKeTiepId = keTiep?.HopDongId,
                MaHopDongKeTiep = keTiep?.MaHopDong,
                CoTheThanhLy = hopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong,
                CoTheGiaHan = hopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong,
                CoBienBanThanhLy = hopDong.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc && keTiep == null,
                ThanhVien = members.Select(m => new ThanhVienDto
                {
                    HoVaTen = m.NguoiThue.HoVaTen,
                    NgayVaoUtc = m.NgayVao,
                    NgayChuyenDiUtc = m.NgayChuyenDi
                }).ToList(),
                DieuKhoan = dieuKhoan.Select(d => d.TieuDe).ToList(),
                ChuoiHopDong = chain.Select(c => new ChuoiHopDongItemDto
                {
                    HopDongId = c.HopDongId,
                    MaHopDong = c.MaHopDong,
                    LoaiText = HopDongCodeChain.LoaiText(c.MaHopDong),
                    TuUtc = c.ThoiDiemBatDau,
                    DenUtc = c.ThoiDiemKetThuc,
                    TrangThaiText = TrangThaiText(c, TimKeTiep(chain, c)),
                    LaHienTai = c.HopDongId == hopDong.HopDongId
                }).ToList()
            });
        }

        public async Task<ServiceResult<LichSuThueDto>> GetLichSuThueAsync(int actorId, int nguoiThueId, CancellationToken ct = default)
        {
            var (scope, nguoiThue, error) = await KiemTraNguoiThueAsync(actorId, nguoiThueId, ct);
            if (error != null)
            {
                return HopDongAccessGuard.As<LichSuThueDto>(error);
            }

            var rows = await _store.GetRentalHistoryAsync(nguoiThueId, scope!.AllowedBranchIds, ct);
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var dungTen = rows.Where(r => r.LaNguoiDungTen).ToList();
            var oGhep = rows.Where(r => !r.LaNguoiDungTen).ToList();

            return ServiceResult<LichSuThueDto>.Ok(new LichSuThueDto
            {
                NguoiThueId = nguoiThue!.NguoiThueId,
                HoVaTen = nguoiThue.HoVaTen,
                CCCD = nguoiThue.CCCD,
                SoDienThoai = nguoiThue.SoDienThoai,
                SoLanThue = dungTen.Select(r => HopDongCodeChain.RootOf(r.MaHopDong)).Distinct().Count(),
                SoHopDong = rows.Count,
                SoLanOGhep = oGhep.Select(r => HopDongCodeChain.RootOf(r.MaHopDong)).Distinct().Count(),
                TongSoThang = TinhTongSoThang(rows, nowUtc),
                NoHienTai = dungTen.Sum(r => r.ConNo),
                Dong = rows.Select(r => new LichSuThueDongDto
                {
                    HopDongId = r.HopDongId,
                    MaHopDong = r.MaHopDong,
                    VaiTro = r.LaNguoiDungTen ? "Đứng tên" : "Ở ghép",
                    LoaiText = HopDongCodeChain.LoaiText(r.MaHopDong),
                    SoPhong = r.SoPhong,
                    TenChiNhanh = r.TenChiNhanh,
                    TuUtc = r.ThoiDiemBatDau,
                    DenUtc = r.ThoiDiemKetThuc,
                    TrangThaiText = TrangThaiText(r.TrangThai),
                    ConNo = r.ConNo
                }).ToList()
            });
        }

        public async Task<ServiceResult<NoCuDto>> GetNoCuAsync(int actorId, int nguoiThueId, CancellationToken ct = default)
        {
            var (scope, _, error) = await KiemTraNguoiThueAsync(actorId, nguoiThueId, ct);
            if (error != null)
            {
                return HopDongAccessGuard.As<NoCuDto>(error);
            }

            var rows = await _store.GetOldDebtsAsync(nguoiThueId, scope!.AllowedBranchIds, ct);
            return ServiceResult<NoCuDto>.Ok(new NoCuDto
            {
                TongNo = rows.Sum(r => r.ConLai),
                HoaDons = rows.Select(r => new NoCuHoaDonDto
                {
                    MaHoaDon = r.MaHoaDon,
                    MaHopDong = r.MaHopDong,
                    Thang = r.Thang,
                    Nam = r.Nam,
                    ConLai = r.ConLai
                }).ToList()
            });
        }

        // Nhân viên không nhìn thấy người thuê của chi nhánh khác thì trả NotFound, không để lộ sự tồn tại.
        private async Task<(EmployeeAccessScope? Scope, NguoiThue? NguoiThue, ServiceResult? Error)> KiemTraNguoiThueAsync(
            int actorId, int nguoiThueId, CancellationToken ct)
        {
            if (actorId <= 0)
            {
                return (null, null, ServiceResult.Fail("Người thực hiện không hợp lệ."));
            }

            var scope = await _access.GetScopeAsync(actorId, ct);
            if (scope == null)
            {
                return (null, null, ServiceResult.Forbidden(HopDongAccessGuard.KhongCoQuyen));
            }

            var nguoiThue = await _store.GetNguoiThueAsync(nguoiThueId, ct);
            if (nguoiThue == null)
            {
                return (scope, null, ServiceResult.NotFound(KhongTimThayNguoiThue));
            }

            if (!scope.IsAdmin && !await _tenantVisibility.AreAllVisibleAsync(new[] { nguoiThueId }, scope.AllowedBranchIds!))
            {
                return (scope, null, ServiceResult.NotFound(KhongTimThayNguoiThue));
            }

            return (scope, nguoiThue, null);
        }

        // Kế tiếp: hợp đồng cùng mã gốc, khác DaHuy, có chỉ số gia hạn nhỏ nhất mà lớn hơn chỉ số hiện tại.
        private static HopDong? TimKeTiep(IEnumerable<HopDong> chain, HopDong hienTai)
        {
            var candidates = chain.Where(c => c.TrangThaiHopDong != TrangThaiHopDong.DaHuy).ToList();
            var maKeTiep = HopDongCodeChain.TimMaKeTiep(hienTai.MaHopDong, candidates.Select(c => c.MaHopDong));
            return maKeTiep == null ? null : candidates.FirstOrDefault(c => c.MaHopDong == maKeTiep);
        }

        private static string TrangThaiText(HopDong hopDong, HopDong? keTiep)
        {
            if (hopDong.TrangThaiHopDong == TrangThaiHopDong.DaKetThuc && keTiep != null)
            {
                return $"Đã gia hạn → {keTiep.MaHopDong}";
            }

            return TrangThaiText((int)hopDong.TrangThaiHopDong);
        }

        private static string TrangThaiText(int trangThai) => trangThai switch
        {
            1 => "Đang hoạt động",
            2 => "Đã kết thúc",
            3 => "Đã hủy",
            _ => string.Empty
        };

        // Đếm theo ngày lịch VN, tính trọn ngày đầu và ngày cuối (ngày kết thúc có thể lưu lúc 00:00 hoặc 23:59:59 VN), cắt ở hôm nay;
        // hợp nhất các khoảng trùng hoặc liền nhau để chỉ đếm một lần, rồi đổi ra tháng (30,4375 ngày/tháng, làm tròn gần nhất).
        private static int TinhTongSoThang(IReadOnlyList<LichSuThueRow> rows, DateTime nowUtc)
        {
            var homNay = DateOnly.FromDateTime(nowUtc.AddHours(7));
            var khoang = rows
                .Select(r =>
                {
                    var tuNgay = DateOnly.FromDateTime(r.ThoiDiemBatDau.AddHours(7));
                    var denNgay = r.ThoiDiemKetThuc.HasValue ? DateOnly.FromDateTime(r.ThoiDiemKetThuc.Value.AddHours(7)) : homNay;
                    return (Tu: tuNgay, Den: denNgay < homNay ? denNgay : homNay);
                })
                .ToList();

            var tongNgay = KhoangNgayThue.HopNhat(khoang).Sum(k => k.Den.DayNumber - k.Tu.DayNumber + 1);

            return (int)Math.Round(tongNgay / 30.4375d, MidpointRounding.AwayFromZero);
        }
    }
}
