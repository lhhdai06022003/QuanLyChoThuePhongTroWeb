using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Services
{
    public class ViecCanLamService : IViecCanLamService
    {
        private readonly IViecCanLamStore _store;
        private readonly IEmployeeAccessService _access;
        private readonly DashboardSettings _dashboardSettings;
        private readonly ViecCanLamSettings _settings;
        private readonly TimeProvider _timeProvider;

        public ViecCanLamService(
            IViecCanLamStore store,
            IEmployeeAccessService access,
            DashboardSettings dashboardSettings,
            ViecCanLamSettings settings,
            TimeProvider timeProvider)
        {
            _store = store;
            _access = access;
            _dashboardSettings = dashboardSettings;
            _settings = settings;
            _timeProvider = timeProvider;
        }

        // Một dòng dữ liệu theo chi nhánh, dùng chung cho mọi loại việc.
        private sealed record DongChiNhanh(int ChiNhanhId, string TenChiNhanh, int SoLuong, int SoLuongGap = 0, int NamCuNhat = 0, decimal TongConNo = 0);

        public async Task<ViecCanLamTongHopDto> GetTongHopAsync(int actorId, int? branchId, CancellationToken ct = default)
        {
            var scope = await _access.GetScopeAsync(actorId, ct);
            var allowed = ResolveAllowed(scope, branchId);
            if (scope == null || (allowed != null && allowed.Count == 0))
            {
                return ViecCanLamTongHopDto.Rong;
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var nowVn = nowUtc.AddHours(7);
            var homNayVnBatDauUtc = DateTime.SpecifyKind(nowVn.Date.AddHours(-7), DateTimeKind.Utc);

            var items = new List<ViecCanLamDto>();

            // CS1
            if (DuocPhep(scope, EmployeeActionCodes.MeterUpload))
            {
                await ThemViecChotChiSoAsync(items, branchId, allowed, nowVn, ct);
            }

            // CS2
            if (DuocPhep(scope, EmployeeActionCodes.MeterReview))
            {
                var rows = await _store.DemAnhChiSoChoXacNhanAsync(branchId, allowed, ct);
                foreach (var kyGroup in rows.GroupBy(r => new { r.Nam, r.Thang }))
                {
                    var dongs = kyGroup.Select(r => new DongChiNhanh(r.ChiNhanhId, r.TenChiNhanh, r.SoLuong)).ToList();
                    ThemViec(items, "CS2", NhomViec.ChiSo, MucUuTienViec.CanLam,
                        $"Ảnh chỉ số chờ xác nhận T{kyGroup.Key.Thang}/{kyGroup.Key.Nam}", dongs, branchId,
                        $"/QuanLyNhaTro/ChotDienNuoc?thang={kyGroup.Key.Thang}&nam={kyGroup.Key.Nam}",
                        thang: kyGroup.Key.Thang, nam: kyGroup.Key.Nam);
                }
            }

            // HD1
            if (DuocPhep(scope, EmployeeActionCodes.InvoiceSubmit))
            {
                var rows = await _store.DemHoaDonNhapAsync(branchId, allowed, ct);
                var dongs = TuHoaDon(rows);
                ThemViec(items, "HD1", NhomViec.HoaDon, MucUuTienViec.CanLam, "Hóa đơn nháp chưa gửi duyệt", dongs, branchId,
                    $"/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={NamCuNhat(dongs)}&trangThaiPhatHanh=0");
            }

            // HD2 (chỉ Admin)
            if (DuocPhep(scope, EmployeeActionCodes.InvoiceFinalize))
            {
                var rows = await _store.DemHoaDonChoDuyetAsync(branchId, allowed, ct);
                var dongs = TuHoaDon(rows);
                ThemViec(items, "HD2", NhomViec.HoaDon, MucUuTienViec.CanLam, "Hóa đơn chờ Admin chốt", dongs, branchId,
                    $"/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={NamCuNhat(dongs)}&trangThaiPhatHanh=1");
            }

            // HD3
            if (DuocPhep(scope, EmployeeActionCodes.InvoiceSend))
            {
                var rows = await _store.DemHoaDonDaChotChuaGuiAsync(branchId, allowed, ct);
                var dongs = TuHoaDon(rows);
                ThemViec(items, "HD3", NhomViec.HoaDon, MucUuTienViec.CanLam, "Hóa đơn đã chốt, chưa gửi khách", dongs, branchId,
                    $"/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={NamCuNhat(dongs)}&trangThaiPhatHanh=2");
            }

            // TT1
            if (DuocPhep(scope, EmployeeActionCodes.PaymentReview))
            {
                var rows = await _store.DemMinhChungChoDoiChieuAsync(branchId, allowed, ct);
                var dongs = rows.Select(r => new DongChiNhanh(r.ChiNhanhId, r.TenChiNhanh, r.SoLuong)).ToList();
                ThemViec(items, "TT1", NhomViec.ThuTien, MucUuTienViec.Khan, "Minh chứng chuyển khoản chờ đối chiếu", dongs, branchId,
                    "/QuanLyNhaTro/DoiChieuThanhToan");
            }

            if (DuocPhep(scope, EmployeeActionCodes.InvoiceRead))
            {
                // TT2
                var quaHan = await _store.DemHoaDonQuaHanAsync(branchId, allowed, nowUtc, ct);
                var dongsQuaHan = TuHoaDon(quaHan);
                ThemViec(items, "TT2", NhomViec.ThuTien, MucUuTienViec.Khan, "Hóa đơn quá hạn", dongsQuaHan, branchId,
                    "/QuanLyNhaTro/ViecCanLam#hoa-don-qua-han", tongConNo: dongsQuaHan.Sum(d => d.TongConNo));

                // TT3
                var chuaToiHan = await _store.DemHoaDonDaGuiChuaToiHanAsync(branchId, allowed, nowUtc, ct);
                var dongsChuaToiHan = TuHoaDon(chuaToiHan);
                ThemViec(items, "TT3", NhomViec.ThuTien, MucUuTienViec.TheoDoi, "Hóa đơn đã gửi, chưa thu đủ", dongsChuaToiHan, branchId,
                    $"/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam={NamCuNhat(dongsChuaToiHan)}&trangThaiPhatHanh=3",
                    tongConNo: dongsChuaToiHan.Sum(d => d.TongConNo));
            }

            // KH1
            {
                var mocGapUtc = nowUtc.AddHours(-_settings.SuCoChoTiepNhanGapSauGio);
                var rows = await _store.DemSuCoChoTiepNhanAsync(branchId, allowed, mocGapUtc, ct);
                var dongs = rows.Select(r => new DongChiNhanh(r.ChiNhanhId, r.TenChiNhanh, r.SoLuong, r.SoLuongGap)).ToList();
                var gap = dongs.Sum(d => d.SoLuongGap);
                ThemViec(items, "KH1", NhomViec.KhachThueHopDong, gap > 0 ? MucUuTienViec.Khan : MucUuTienViec.CanLam,
                    "Sự cố chờ tiếp nhận", dongs, branchId, "/QuanLyNhaTro/YeuCauSuCo?trangThai=0&soThang=0",
                    ghiChuThem: gap > 0 ? $"{gap} sự cố quá {_settings.SuCoChoTiepNhanGapSauGio} giờ" : null);
            }

            // KH2
            {
                var mocUtc = nowUtc.AddDays(-_settings.SuCoXuLyQuaLauSauNgay);
                var rows = await _store.DemSuCoXuLyQuaLauAsync(branchId, allowed, mocUtc, ct);
                var dongs = rows.Select(r => new DongChiNhanh(r.ChiNhanhId, r.TenChiNhanh, r.SoLuong)).ToList();
                ThemViec(items, "KH2", NhomViec.KhachThueHopDong, MucUuTienViec.CanLam, "Sự cố xử lý quá lâu", dongs, branchId,
                    "/QuanLyNhaTro/YeuCauSuCo?trangThai=1&soThang=0");
            }

            // KH3
            {
                var denTruocUtc = homNayVnBatDauUtc.AddDays(_settings.HopDongSapHetHanTrongNgay + 1);
                var gapTruocUtc = homNayVnBatDauUtc.AddDays(_settings.HopDongSapHetHanGapTrongNgay + 1);
                var rows = await _store.DemHopDongSapHetHanAsync(branchId, allowed, homNayVnBatDauUtc, denTruocUtc, gapTruocUtc, ct);
                var dongs = rows.Select(r => new DongChiNhanh(r.ChiNhanhId, r.TenChiNhanh, r.SoLuong, r.SoLuongGap)).ToList();
                var gap = dongs.Sum(d => d.SoLuongGap);
                ThemViec(items, "KH3", NhomViec.KhachThueHopDong, gap > 0 ? MucUuTienViec.CanLam : MucUuTienViec.TheoDoi,
                    "Hợp đồng sắp hết hạn", dongs, branchId, "/QuanLyNhaTro/QuanLyHopDong",
                    ghiChuThem: gap > 0 ? $"{gap} hợp đồng còn tối đa {_settings.HopDongSapHetHanGapTrongNgay} ngày" : null);
            }

            var sapXep = items
                .OrderBy(i => i.MucUuTien)
                .ThenByDescending(i => i.SoLuong)
                .ThenBy(i => i.MaViec, StringComparer.Ordinal)
                .ThenBy(i => i.Nam ?? 0)
                .ThenBy(i => i.Thang ?? 0)
                .ToList();

            return new ViecCanLamTongHopDto
            {
                TongSo = sapXep.Sum(i => i.SoLuong),
                SoKhan = sapXep.Where(i => i.MucUuTien == MucUuTienViec.Khan).Sum(i => i.SoLuong),
                SoCanLam = sapXep.Where(i => i.MucUuTien == MucUuTienViec.CanLam).Sum(i => i.SoLuong),
                SoTheoDoi = sapXep.Where(i => i.MucUuTien == MucUuTienViec.TheoDoi).Sum(i => i.SoLuong),
                Items = sapXep
            };
        }

        public async Task<DanhSachHoaDonQuaHanDto> GetHoaDonQuaHanAsync(int actorId, int? branchId, CancellationToken ct = default)
        {
            var scope = await _access.GetScopeAsync(actorId, ct);
            var allowed = ResolveAllowed(scope, branchId);
            if (scope == null || (allowed != null && allowed.Count == 0))
            {
                return new DanhSachHoaDonQuaHanDto();
            }

            if (!DuocPhep(scope, EmployeeActionCodes.InvoiceRead))
            {
                return new DanhSachHoaDonQuaHanDto();
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var nowVn = nowUtc.AddHours(7);

            var rows = await _store.GetHoaDonQuaHanAsync(branchId, allowed, nowUtc, _settings.SoDongHoaDonQuaHan, ct);
            var dem = await _store.DemHoaDonQuaHanAsync(branchId, allowed, nowUtc, ct);

            return new DanhSachHoaDonQuaHanDto
            {
                TongSo = dem.Sum(d => d.SoLuong),
                Items = rows.Select(r => new HoaDonQuaHanDto
                {
                    HoaDonId = r.HoaDonId,
                    MaHoaDon = r.MaHoaDon,
                    SoPhong = r.SoPhong,
                    TenChiNhanh = r.TenChiNhanh,
                    KhachThue = r.KhachThue,
                    SoConNo = r.TongTien - r.DaThu,
                    HanThanhToanUtc = r.HanThanhToan,
                    SoNgayQuaHan = (nowVn.Date - r.HanThanhToan.AddHours(7).Date).Days
                }).ToList()
            };
        }

        // Trả về danh sách chi nhánh để lọc (null = Admin không giới hạn). Danh sách rỗng nghĩa là
        // không được xem dữ liệu (actor không hợp lệ, chi nhánh ngoài phạm vi, chưa được phân công).
        private static IReadOnlyCollection<int>? ResolveAllowed(EmployeeAccessScope? scope, int? branchId)
        {
            if (scope == null)
            {
                return Array.Empty<int>();
            }

            if (branchId.HasValue && !scope.CanAccessBranch(branchId.Value))
            {
                return Array.Empty<int>();
            }

            var allowed = scope.AllowedBranchIds;
            if (allowed != null && allowed.Count == 0)
            {
                return Array.Empty<int>();
            }

            return allowed;
        }

        private static bool DuocPhep(EmployeeAccessScope scope, string maHanhDong)
            => scope.IsAdmin || !EmployeeActionCodes.IsAdminOnly(maHanhDong);

        private async Task ThemViecChotChiSoAsync(List<ViecCanLamDto> items, int? branchId, IReadOnlyCollection<int>? allowed, DateTime nowVn, CancellationToken ct)
        {
            var (thangMucTieu, namMucTieu) = TinhKyMucTieu(nowVn, _dashboardSettings.ChotDienNuocDay);
            var soKy = Math.Max(1, _settings.SoKyQuetChiSo);
            var kyMucTieu = new DateTime(namMucTieu, thangMucTieu, 1);
            var kyDau = kyMucTieu.AddMonths(-(soKy - 1));

            var hopDongs = await _store.GetHopDongDangHoatDongAsync(branchId, allowed, ct);
            var daGhi = await _store.GetKyChiSoDaChotAsync(branchId, allowed, kyDau.Month, kyDau.Year, ct);

            for (var ky = kyDau; ky <= kyMucTieu; ky = ky.AddMonths(1))
            {
                var m = ky.Month;
                var y = ky.Year;

                var daGhiPhongs = daGhi
                    .Where(r => r.Thang == m && r.Nam == y)
                    .Select(r => r.PhongTroId)
                    .ToHashSet();

                var phongThieu = hopDongs
                    .Where(c =>
                    {
                        var batDau = c.ThoiDiemBatDau.AddHours(7);
                        return new DateTime(batDau.Year, batDau.Month, 1) <= ky;
                    })
                    .Where(c => !daGhiPhongs.Contains(c.PhongTroId))
                    .GroupBy(c => new { c.ChiNhanhId, c.TenChiNhanh })
                    .Select(g => new DongChiNhanh(g.Key.ChiNhanhId, g.Key.TenChiNhanh, g.Select(c => c.PhongTroId).Distinct().Count()))
                    .ToList();

                var laKyMucTieu = ky == kyMucTieu;
                ThemViec(items, "CS1", NhomViec.ChiSo, laKyMucTieu ? MucUuTienViec.CanLam : MucUuTienViec.Khan,
                    $"Phòng chưa chốt điện nước T{m}/{y}", phongThieu, branchId,
                    $"/QuanLyNhaTro/ChotDienNuoc?thang={m}&nam={y}", thang: m, nam: y);
            }
        }

        // Kỳ mục tiêu: lùi 2 tháng nếu chưa tới ngày chốt, ngược lại lùi 1 tháng (theo giờ VN).
        internal static (int Thang, int Nam) TinhKyMucTieu(DateTime nowVn, int chotDienNuocDay)
        {
            var thang = nowVn.Month;
            var nam = nowVn.Year;
            thang -= nowVn.Day < chotDienNuocDay ? 2 : 1;
            while (thang < 1)
            {
                thang += 12;
                nam -= 1;
            }

            return (thang, nam);
        }

        private static List<DongChiNhanh> TuHoaDon(IReadOnlyList<DemHoaDonRow> rows)
            => rows.Select(r => new DongChiNhanh(r.ChiNhanhId, r.TenChiNhanh, r.SoLuong, 0, r.NamCuNhat, r.TongConNo)).ToList();

        private static int NamCuNhat(List<DongChiNhanh> dongs)
            => dongs.Count == 0 ? 0 : dongs.Min(d => d.NamCuNhat);

        private static void ThemViec(
            List<ViecCanLamDto> items,
            string maViec,
            NhomViec nhom,
            MucUuTienViec mucUuTien,
            string tieuDe,
            List<DongChiNhanh> dongs,
            int? branchId,
            string duongDan,
            int? thang = null,
            int? nam = null,
            decimal? tongConNo = null,
            string? ghiChuThem = null)
        {
            var tong = dongs.Sum(d => d.SoLuong);
            if (tong <= 0)
            {
                return;
            }

            var phanChiNhanh = dongs.Where(d => d.SoLuong > 0).ToList();
            var top = phanChiNhanh
                .OrderByDescending(d => d.SoLuong)
                .ThenBy(d => d.TenChiNhanh, StringComparer.Ordinal)
                .ToList();

            var moTa = string.Join(" · ", top.Take(3).Select(d => $"{d.TenChiNhanh}: {d.SoLuong}"));
            if (top.Count > 3)
            {
                moTa += $" · và {top.Count - 3} chi nhánh khác";
            }

            if (tongConNo.HasValue)
            {
                moTa += $" · còn nợ {TienTe.DinhDang(tongConNo.Value)}";
            }

            if (!string.IsNullOrEmpty(ghiChuThem))
            {
                moTa += $" · {ghiChuThem}";
            }

            var chiNhanhIds = phanChiNhanh.Select(d => d.ChiNhanhId).Distinct().ToList();
            var chiNhanhIdLink = branchId ?? (chiNhanhIds.Count == 1 ? chiNhanhIds[0] : (int?)null);

            items.Add(new ViecCanLamDto
            {
                MaViec = maViec,
                Nhom = nhom,
                MucUuTien = mucUuTien,
                TieuDe = tieuDe,
                MoTa = moTa,
                SoLuong = tong,
                Link = GhepLink(duongDan, chiNhanhIdLink),
                Thang = thang,
                Nam = nam,
                TongConNo = tongConNo
            });
        }

        // Gắn chiNhanhId trước dấu '#'.
        private static string GhepLink(string duongDan, int? chiNhanhId)
        {
            if (!chiNhanhId.HasValue)
            {
                return duongDan;
            }

            var viTriNeo = duongDan.IndexOf('#');
            var phanChinh = viTriNeo >= 0 ? duongDan[..viTriNeo] : duongDan;
            var neo = viTriNeo >= 0 ? duongDan[viTriNeo..] : string.Empty;
            var noi = phanChinh.Contains('?') ? "&" : "?";
            return $"{phanChinh}{noi}chiNhanhId={chiNhanhId.Value}{neo}";
        }
    }
}
