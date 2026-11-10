using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class DashboardServiceTests
    {
        private readonly FakeDashboardStore _store = new();
        private readonly FakeEmployeeAccessService _access = new();
        private readonly DashboardService _service;

        public DashboardServiceTests()
        {
            // 05/11/2026 03:00Z = 10:00 giờ VN.
            var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero));
            _service = new DashboardService(_store, _access, time);
            _store.CongNo = new DashboardCongNoDto { ChoThuThang = 700_000m, SoPhongDangNo = 3, SoHoaDonQuaHan = 2, TienQuaHan = 811_000m };
            _store.MonthlyStats.Add(new DashboardInvoiceStatDto { Thang = 11, DaThu = 500_000m, ChoThu = 700_000m });
        }

        private void DatAdmin() => _access.ScopeToReturn = new EmployeeAccessScope(1, true);

        private void DatNhanVien(params int[] chiNhanhs) => _access.ScopeToReturn = new EmployeeAccessScope(10, false, chiNhanhs);

        [Fact]
        public async Task NhanVien_KhongXemDoanhThu_VaKhongChayTruyVanDoanhThu()
        {
            DatNhanVien(1);

            var dto = await _service.GetDashboardDataAsync(10, null, 2026, 11);

            Assert.False(dto.CoXemDoanhThu);
            Assert.Null(dto.DoanhThuThangNay);
            Assert.Null(dto.ChartData);
            Assert.Null(dto.ChartDataChoThu);
            Assert.Equal(0, _store.GetInvoiceMonthlyStatsCalls);
            Assert.Equal(1, _store.GetCongNoCalls);
            Assert.Equal(700_000m, dto.TongTienChoThu);
            Assert.Equal(811_000m, dto.TienQuaHan);
            Assert.Equal(2, dto.SoHoaDonQuaHan);
            Assert.Equal(3, dto.SoPhongChuaThanhToan);
            Assert.Equal(12, dto.ChartLabels.Count);
        }

        // "Hoạt động gần đây" ghi kênh thu theo PaymentChannels (SePay/VNPay không còn gộp vào "Chuyển khoản").
        [Fact]
        public async Task HoatDongGanDay_GhiKenhThuTheoMaGiaoDich()
        {
            DatNhanVien(1);
            var luc = new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);
            _store.RecentPayments.Add(new DashboardRecentPaymentDto { NgayThanhToan = luc, SoPhong = "P1", SoTienThanhToan = 1m, PhuongThuc = PhuongThucThanhToan.ChuyenKhoan, MaGiaoDich = "SEPAY-A1" });
            _store.RecentPayments.Add(new DashboardRecentPaymentDto { NgayThanhToan = luc.AddMinutes(-1), SoPhong = "P2", SoTienThanhToan = 1m, PhuongThuc = PhuongThucThanhToan.ChuyenKhoan, MaGiaoDich = "VNPAY-9" });
            _store.RecentPayments.Add(new DashboardRecentPaymentDto { NgayThanhToan = luc.AddMinutes(-2), SoPhong = "P3", SoTienThanhToan = 1m, PhuongThuc = PhuongThucThanhToan.ChuyenKhoan, MaGiaoDich = "CK-1" });
            _store.RecentPayments.Add(new DashboardRecentPaymentDto { NgayThanhToan = luc.AddMinutes(-3), SoPhong = "P4", SoTienThanhToan = 1m, PhuongThuc = PhuongThucThanhToan.TienMat, MaGiaoDich = null });

            var dto = await _service.GetDashboardDataAsync(10, null, 2026, 11);

            var moTa = dto.RecentActivities.Select(a => a.Description).ToList();
            Assert.EndsWith("- SePay", moTa[0]);
            Assert.EndsWith("- VNPay", moTa[1]);
            Assert.EndsWith("- Chuyển khoản", moTa[2]);
            Assert.EndsWith("- Tiền mặt", moTa[3]);
        }

        [Fact]
        public async Task Admin_XemDoanhThu_GoiTruyVanDoanhThuMotLan()
        {
            DatAdmin();

            var dto = await _service.GetDashboardDataAsync(1, null, 2026, 11);

            Assert.True(dto.CoXemDoanhThu);
            Assert.Equal(500_000m, dto.DoanhThuThangNay);
            Assert.Equal(1, _store.GetInvoiceMonthlyStatsCalls);
            Assert.Equal(1, _store.GetCongNoCalls);
            Assert.Equal(12, dto.ChartData!.Count);
            Assert.Equal(500_000m, dto.ChartData[10]);
            Assert.Equal(700_000m, dto.ChartDataChoThu![10]);
            Assert.Null(_store.LastAllowedBranchIds);
        }

        [Fact]
        public async Task ScopeNull_NhuNhanVienKhongChiNhanh_FailClosed()
        {
            _access.ScopeToReturn = null;

            var dto = await _service.GetDashboardDataAsync(99, null, 2026, 11);

            Assert.False(dto.CoXemDoanhThu);
            Assert.Null(dto.DoanhThuThangNay);
            Assert.Equal(0, _store.GetInvoiceMonthlyStatsCalls);
            Assert.NotNull(_store.LastAllowedBranchIds);
            Assert.Empty(_store.LastAllowedBranchIds!);
        }

        // Fail closed: không có phạm vi thì không doanh thu, không bảng tiền mặt theo người, truy vấn nhận danh sách rỗng.
        [Fact]
        public async Task ScopeNull_TienMatVaBangTheoNguoi_FailClosed()
        {
            _access.ScopeToReturn = null;
            _store.TienMat.Add(new DashboardTienMatNguoiDto { NguoiDungId = 99, Ten = "x", SoKhoan = 2, TongTien = 1_000m });

            var dto = await _service.GetDashboardDataAsync(99, null, 2026, 11);

            Assert.False(dto.CoXemDoanhThu);
            Assert.Null(dto.TienMatNhanVien);
            Assert.Equal(0, _store.GetInvoiceMonthlyStatsCalls);
            Assert.Equal(99, _store.LastTienMatArgs!.Value.NguoiXacNhanId);
            Assert.Empty(_store.LastAllowedBranchIds!);
        }

        // Nhân viên chưa được phân công chi nhánh nào: không doanh thu, store nhận danh sách rỗng.
        [Fact]
        public async Task NhanVienChuaPhanCong_KhongDoanhThu_StoreNhanDanhSachRong()
        {
            _access.ScopeToReturn = new EmployeeAccessScope(10, false);

            var dto = await _service.GetDashboardDataAsync(10, null, 2026, 11);

            Assert.False(dto.CoXemDoanhThu);
            Assert.Null(dto.DoanhThuThangNay);
            Assert.Null(dto.TienMatNhanVien);
            Assert.Equal(0, _store.GetInvoiceMonthlyStatsCalls);
            Assert.Empty(_store.LastAllowedBranchIds!);
        }

        [Fact]
        public async Task NhanVien_ChonChiNhanhNgoaiPhamVi_StoreNhanDanhSachRong()
        {
            DatNhanVien(1);

            await _service.GetDashboardDataAsync(10, 2, 2026, 11);

            Assert.Empty(_store.LastAllowedBranchIds!);
            Assert.Equal(2, _store.LastBranchId);
        }

        [Fact]
        public async Task NhanVien_ChiNhanhCuaMinh_StoreNhanDungDanhSach()
        {
            DatNhanVien(1, 2);

            await _service.GetDashboardDataAsync(10, null, 2026, 11);

            Assert.Equal(new[] { 1, 2 }, _store.LastAllowedBranchIds!.OrderBy(x => x).ToArray());
        }

        [Fact]
        public async Task CongNo_NhanDungKyVaThoiDiemHienTai()
        {
            DatAdmin();

            await _service.GetDashboardDataAsync(1, null, 2026, 9);

            Assert.Equal((2026, 9, new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc)), _store.LastCongNoArgs);
        }

        // ----- Tiền mặt đã thu trong tháng (giờ VN) -----

        [Fact]
        public async Task TienMat_BienThangGioVn_Thang11VaThang12()
        {
            DatAdmin();

            await _service.GetDashboardDataAsync(1, null, 2026, 11);
            Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), _store.LastTienMatArgs!.Value.TuUtc);
            Assert.Equal(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc), _store.LastTienMatArgs.Value.DenUtc);

            await _service.GetDashboardDataAsync(1, null, 2026, 12);
            Assert.Equal(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc), _store.LastTienMatArgs!.Value.TuUtc);
            Assert.Equal(new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc), _store.LastTienMatArgs.Value.DenUtc);
        }

        [Fact]
        public async Task TienMat_NhanVien_ChiLayCuaMinh_VaKhongCoBangTheoNguoi()
        {
            DatNhanVien(1);
            _store.TienMat.Add(new DashboardTienMatNguoiDto { NguoiDungId = 10, Ten = "nv", SoKhoan = 3, TongTien = 900_000m });
            _store.TienMat.Add(new DashboardTienMatNguoiDto { NguoiDungId = 11, Ten = "nv2", SoKhoan = 1, TongTien = 5_000m });

            var dto = await _service.GetDashboardDataAsync(10, null, 2026, 11);

            Assert.Equal(10, _store.LastTienMatArgs!.Value.NguoiXacNhanId);
            Assert.Null(dto.TienMatNhanVien);
            Assert.Equal(3, dto.TienMatCuaToi.SoKhoan);
            Assert.Equal(900_000m, dto.TienMatCuaToi.TongTien);
        }

        [Fact]
        public async Task TienMat_NhanVienKhongCoKhoan_LaKhong()
        {
            DatNhanVien(1);

            var dto = await _service.GetDashboardDataAsync(10, null, 2026, 11);

            Assert.Equal(0, dto.TienMatCuaToi.SoKhoan);
            Assert.Equal(0m, dto.TienMatCuaToi.TongTien);
        }

        [Fact]
        public async Task TienMat_Admin_GomTheoNguoi_SapTheoTongTienGiamDan_VaTuLayDongCuaMinh()
        {
            DatAdmin();
            _store.TienMat.Add(new DashboardTienMatNguoiDto { NguoiDungId = 20, Ten = "b", SoKhoan = 1, TongTien = 100m });
            _store.TienMat.Add(new DashboardTienMatNguoiDto { NguoiDungId = 1, Ten = "admin", SoKhoan = 2, TongTien = 700m });
            _store.TienMat.Add(new DashboardTienMatNguoiDto { NguoiDungId = 21, Ten = "a", SoKhoan = 1, TongTien = 100m });

            var dto = await _service.GetDashboardDataAsync(1, null, 2026, 11);

            Assert.Null(_store.LastTienMatArgs!.Value.NguoiXacNhanId);
            Assert.Equal(1, _store.GetTienMatCalls);
            Assert.Equal(new[] { 1, 21, 20 }, dto.TienMatNhanVien!.Select(n => n.NguoiDungId).ToArray());
            Assert.Equal(2, dto.TienMatCuaToi.SoKhoan);
            Assert.Equal(700m, dto.TienMatCuaToi.TongTien);
        }

        [Fact]
        public async Task TienMat_ThangNgoaiKhoang_KhongGoiStore()
        {
            DatNhanVien(1);
            var nv = await _service.GetDashboardDataAsync(10, null, 2026, 13);
            Assert.Equal(0, _store.GetTienMatCalls);
            Assert.Null(nv.TienMatNhanVien);
            Assert.Equal(0m, nv.TienMatCuaToi.TongTien);

            DatAdmin();
            var admin = await _service.GetDashboardDataAsync(1, null, 1999, 5);
            Assert.Equal(0, _store.GetTienMatCalls);
            Assert.Empty(admin.TienMatNhanVien!);
        }

        [Fact]
        public async Task TienMat_NhanVienChonChiNhanhNgoaiPhamVi_StoreNhanDanhSachRong()
        {
            DatNhanVien(1);

            await _service.GetDashboardDataAsync(10, 2, 2026, 11);

            Assert.Empty(_store.LastAllowedBranchIds!);
        }
    }
}
