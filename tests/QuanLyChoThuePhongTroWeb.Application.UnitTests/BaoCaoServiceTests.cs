using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class BaoCaoServiceTests
    {
        private readonly FakeBaoCaoStore _store = new();
        private readonly FakeEmployeeAccessService _access = new();
        private readonly FakeBaoCaoExcelExporter _exporter = new();
        private readonly BaoCaoService _service;

        public BaoCaoServiceTests()
        {
            // 05/11/2026 03:00Z = 10:00 giờ VN.
            var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero));
            _service = new BaoCaoService(_store, _access, _exporter, time);
            _store.TenChiNhanh[1] = "Chi nhánh A";
            _store.TenChiNhanh[2] = "Chi nhánh B";
        }

        private static BaoCaoFilter Filter(int? chiNhanhId = null, int tuThang = 1, int tuNam = 2026, int denThang = 3, int denNam = 2026) =>
            new() { ChiNhanhId = chiNhanhId, TuThang = tuThang, TuNam = tuNam, DenThang = denThang, DenNam = denNam };

        private void DatAdmin() => _access.ScopeToReturn = new EmployeeAccessScope(1, true);

        private void DatNhanVien(params int[] chiNhanhs)
        {
            _access.ScopeToReturn = new EmployeeAccessScope(10, false, chiNhanhs);
            foreach (var id in chiNhanhs)
            {
                _access.Permissions.Add((10, id, EmployeeActionCodes.ReportRead));
            }
        }

        [Fact]
        public async Task ActorKhongHopLe_BiTuChoi_VaKhongGoiStore()
        {
            DatAdmin();
            var r = await _service.GetDoanhThuAsync(0, Filter());
            Assert.False(r.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task ScopeNull_BiTuChoi()
        {
            _access.ScopeToReturn = null;
            var r = await _service.GetCongNoAsync(5, Filter());
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task NhanVienChuaPhanCong_BiTuChoi()
        {
            _access.ScopeToReturn = new EmployeeAccessScope(10, false);
            var r = await _service.GetHopDongAsync(10, Filter());
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal("Bạn chưa được phân công chi nhánh nào.", r.Message);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task NhanVienChonChiNhanhKhongDuocGiao_BiTuChoi_VaKhongGoiStore()
        {
            DatNhanVien(1);
            var r = await _service.GetDoanhThuAsync(10, Filter(chiNhanhId: 2));
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task NhanVienThieuQuyenReportRead_BiTuChoi()
        {
            _access.ScopeToReturn = new EmployeeAccessScope(10, false, new[] { 1 });
            var r = await _service.GetDienNuocAsync(10, Filter(chiNhanhId: 1));
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task AdminKhongChonChiNhanh_StoreNhanAllowedNull()
        {
            DatAdmin();
            var r = await _service.GetDoanhThuAsync(1, Filter());
            Assert.True(r.Success);
            Assert.Null(_store.LastAllowedBranchIds);
            Assert.Null(_store.LastChiNhanhId);
            Assert.Equal(202601, _store.LastTuKyKey);
            Assert.Equal(202603, _store.LastDenKyKey);
        }

        [Fact]
        public async Task NhanVienKhongChonChiNhanh_StoreNhanDungDanhSach()
        {
            DatNhanVien(1, 2);
            var r = await _service.GetHopDongAsync(10, Filter());
            Assert.True(r.Success);
            Assert.NotNull(_store.LastAllowedBranchIds);
            Assert.Equal(new[] { 1, 2 }, _store.LastAllowedBranchIds!.OrderBy(x => x).ToArray());
        }

        [Fact]
        public async Task KhoangHon24Ky_BiTuChoiLoaiValidation()
        {
            DatAdmin();
            var r = await _service.GetDoanhThuAsync(1, Filter(tuThang: 1, tuNam: 2024, denThang: 1, denNam: 2026));
            Assert.False(r.Success);
            Assert.Equal(ServiceErrorKind.Validation, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task TabCongNo_KhoangSaiVanThanhCong()
        {
            DatAdmin();
            var r = await _service.GetCongNoAsync(1, Filter(tuThang: 13, tuNam: 1900, denThang: 1, denNam: 1800));
            Assert.True(r.Success);
            Assert.Equal("", r.Data!.PhamVi.TuKyText);
        }

        [Fact]
        public async Task AdminChonChiNhanhKhongTonTai_NotFound()
        {
            DatAdmin();
            _access.Permissions.Add((1, 99, EmployeeActionCodes.ReportRead));
            var r = await _service.GetDoanhThuAsync(1, Filter(chiNhanhId: 99));
            Assert.Equal(ServiceErrorKind.NotFound, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task ChiNhanhKhongDuong_BiTuChoi()
        {
            DatAdmin();
            var r = await _service.GetDoanhThuAsync(1, Filter(chiNhanhId: -1));
            Assert.Equal(ServiceErrorKind.Validation, r.ErrorKind);
            Assert.Equal("Chi nhánh không hợp lệ.", r.Message);
        }

        [Fact]
        public async Task PhamVi_TenChiNhanhVaThoiDiemDung()
        {
            DatAdmin();
            var admin = await _service.GetDoanhThuAsync(1, Filter());
            Assert.Equal("Tất cả chi nhánh", admin.Data!.PhamVi.TenChiNhanh);
            Assert.Equal("T1/2026", admin.Data.PhamVi.TuKyText);
            Assert.Equal("T3/2026", admin.Data.PhamVi.DenKyText);
            Assert.Equal("05/11/2026 10:00", admin.Data.PhamVi.ThoiDiemTinhText);

            DatNhanVien(1, 2);
            var nv = await _service.GetCongSuatPhongAsync(10, Filter());
            Assert.Equal("Tất cả chi nhánh của tôi", nv.Data!.PhamVi.TenChiNhanh);

            var mot = await _service.GetCongSuatPhongAsync(10, Filter(chiNhanhId: 2));
            Assert.Equal("Chi nhánh B", mot.Data!.PhamVi.TenChiNhanh);
            Assert.Equal(2, mot.Data.PhamVi.ChiNhanhId);
            Assert.Empty(mot.Data.TheoChiNhanh);
        }

        [Fact]
        public async Task XuatExcel_GoiDungExporterTheoTab_VaTenFileDungMau()
        {
            DatAdmin();
            var cases = new[]
            {
                (BaoCaoTab.DoanhThu, "XuatDoanhThu", "doanh-thu"),
                (BaoCaoTab.CongNo, "XuatCongNo", "cong-no"),
                (BaoCaoTab.CongSuat, "XuatCongSuatPhong", "cong-suat"),
                (BaoCaoTab.HopDong, "XuatHopDong", "hop-dong"),
                (BaoCaoTab.DienNuoc, "XuatDienNuoc", "dien-nuoc")
            };

            foreach (var (tab, method, slug) in cases)
            {
                _exporter.Calls.Clear();
                var r = await _service.XuatExcelAsync(1, tab, Filter(tuThang: 11, tuNam: 2025, denThang: 2, denNam: 2026));
                Assert.True(r.Success);
                Assert.Equal(new[] { method }, _exporter.Calls.ToArray());
                Assert.Equal($"BaoCao_{slug}_202511_202602.xlsx", r.Data!.TenFile);
                Assert.NotEmpty(r.Data.NoiDung);
            }
        }

        [Fact]
        public async Task XuatExcel_LoiQuyen_KhongGoiExporter()
        {
            DatNhanVien(1);
            var r = await _service.XuatExcelAsync(10, BaoCaoTab.DoanhThu, Filter(chiNhanhId: 2));
            Assert.False(r.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Empty(_exporter.Calls);
        }

        [Fact]
        public async Task NhanVien_DoanhThu_BiTuChoi_VaKhongGoiStore()
        {
            DatNhanVien(1);
            var khongChon = await _service.GetDoanhThuAsync(10, Filter());
            var cuaMinh = await _service.GetDoanhThuAsync(10, Filter(chiNhanhId: 1));
            Assert.Equal(ServiceErrorKind.Forbidden, khongChon.ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, cuaMinh.ErrorKind);
            Assert.Equal("Chỉ Admin được xem báo cáo doanh thu.", khongChon.Message);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task NhanVien_DoanhThuKhoangSai_Tra403TruocValidation()
        {
            DatNhanVien(1);
            var r = await _service.GetDoanhThuAsync(10, Filter(tuThang: 1, tuNam: 2024, denThang: 1, denNam: 2026));
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task NhanVien_XuatExcelDoanhThu_BiTuChoi_KhongGoiExporter()
        {
            DatNhanVien(1);
            var r = await _service.XuatExcelAsync(10, BaoCaoTab.DoanhThu, Filter(chiNhanhId: 1));
            Assert.False(r.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Empty(_exporter.Calls);
            Assert.Equal(0, _store.CallCount);
        }

        // Fail closed: IEmployeeAccessService không trả phạm vi thì doanh thu bị từ chối, store/exporter không được gọi.
        [Fact]
        public async Task ScopeNull_DoanhThuVaXuatExcelDoanhThu_BiTuChoi_KhongGoiStoreHayExporter()
        {
            _access.ScopeToReturn = null;

            var data = await _service.GetDoanhThuAsync(5, Filter());
            var excel = await _service.XuatExcelAsync(5, BaoCaoTab.DoanhThu, Filter());

            Assert.Equal(ServiceErrorKind.Forbidden, data.ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, excel.ErrorKind);
            Assert.Equal(0, _store.CallCount);
            Assert.Empty(_exporter.Calls);
        }

        // Nhân viên chưa phân công vào tab Doanh thu nhận 403 "chỉ Admin" (không phải thông báo chưa phân công).
        [Fact]
        public async Task NhanVienChuaPhanCong_DoanhThu_Tra403ChiAdmin()
        {
            _access.ScopeToReturn = new EmployeeAccessScope(10, false);

            var r = await _service.GetDoanhThuAsync(10, Filter());

            Assert.Equal(ServiceErrorKind.Forbidden, r.ErrorKind);
            Assert.Equal("Chỉ Admin được xem báo cáo doanh thu.", r.Message);
            Assert.Equal(0, _store.CallCount);
        }

        [Fact]
        public async Task NhanVien_CacTabKhac_VanThanhCong()
        {
            DatNhanVien(1);
            Assert.True((await _service.GetCongNoAsync(10, Filter())).Success);
            Assert.True((await _service.GetCongSuatPhongAsync(10, Filter())).Success);
            Assert.True((await _service.GetHopDongAsync(10, Filter())).Success);
            Assert.True((await _service.GetDienNuocAsync(10, Filter())).Success);
        }

        [Fact]
        public void BaoCaoTabs_ParseKhongPhanBietHoaThuong()
        {
            Assert.True(BaoCaoTabs.TryParse("Doanh-Thu", out var tab));
            Assert.Equal(BaoCaoTab.DoanhThu, tab);
            Assert.False(BaoCaoTabs.TryParse(null, out _));
            Assert.False(BaoCaoTabs.TryParse("", out _));
            Assert.False(BaoCaoTabs.TryParse("xyz", out _));
            Assert.Equal("dien-nuoc", BaoCaoTabs.Slug(BaoCaoTab.DienNuoc));
        }
    }
}
