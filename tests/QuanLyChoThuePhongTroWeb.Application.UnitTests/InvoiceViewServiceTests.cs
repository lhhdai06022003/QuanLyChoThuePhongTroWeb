using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoiceViewServiceTests
    {
        private class TestInvoiceDocumentExporter : IInvoiceDocumentExporter
        {
            public byte[]? LastQrBytes { get; private set; }
            public HoaDonChiTietRes? LastHoaDon { get; private set; }

            public byte[] ExportExcel(HoaDonChiTietRes hoaDon)
            {
                LastHoaDon = hoaDon;
                return new byte[] { 1, 2, 3 };
            }

            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName)
            {
                LastHoaDon = hoaDon;
                LastQrBytes = qrBytes;
                return new byte[] { 4, 5, 6 };
            }
        }

        private class TestVietQRService : IVietQRService
        {
            public string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo) => "dummy_qr";
            public byte[] GenerateQRCodePNGBytes(string payload) => new byte[] { 7, 8, 9 };
        }

        private readonly FakeInvoiceViewStore _viewStore = new();
        private readonly FakeHoaDonStore _hoaDonStore = new();
        private readonly FakeEmployeeAccessService _accessService = new();
        private readonly TestInvoiceDocumentExporter _exporter = new();
        private readonly TestVietQRService _vietQr = new();
        private readonly VietQrSettings _settings = new() { BankId = "MB", AccountNumber = "123456", AccountName = "CHU TRO" };

        private InvoiceViewService CreateService()
        {
            return new InvoiceViewService(
                _viewStore,
                _hoaDonStore,
                _accessService,
                _exporter,
                _vietQr,
                _settings);
        }

        [Fact]
        public async Task List_StaffWithoutAssignment_Empty_DrawPreserved()
        {
            var service = CreateService();
            _accessService.ScopeToReturn = new EmployeeAccessScope(1, false, new List<int>());

            var req = new DataTableRequest { Draw = 42, Start = 0, Length = 10 };
            var filter = new InvoiceListFilter { ChiNhanhId = 0 };

            var res = await service.GetEmployeeListAsync(req, filter, 1);

            Assert.Equal(42, res.draw);
            Assert.Equal(0, res.recordsTotal);
            Assert.Empty(res.data);
        }

        [Fact]
        public async Task List_StaffOtherBranchFilter_Empty()
        {
            var service = CreateService();
            _accessService.ScopeToReturn = new EmployeeAccessScope(1, false, new List<int> { 10 }); // only branch 10

            var req = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };
            var filter = new InvoiceListFilter { ChiNhanhId = 20 }; // branch 20

            var res = await service.GetEmployeeListAsync(req, filter, 1);

            Assert.Equal(0, res.recordsTotal);
            Assert.Empty(res.data);
        }

        [Fact]
        public async Task List_Admin_PassesNullAllowedBranches()
        {
            var service = CreateService();
            _accessService.ScopeToReturn = new EmployeeAccessScope(99, true);

            var req = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };
            var filter = new InvoiceListFilter { ChiNhanhId = 0 };

            await service.GetEmployeeListAsync(req, filter, 99);

            Assert.Null(_viewStore.LastAllowedBranchIds);
        }

        [Fact]
        public async Task List_StaffNoBranchFilter_PassesActiveBranches()
        {
            var service = CreateService();
            var activeBranches = new List<int> { 10, 20 };
            _accessService.ScopeToReturn = new EmployeeAccessScope(1, false, activeBranches);

            var req = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };
            var filter = new InvoiceListFilter { ChiNhanhId = 0 };

            await service.GetEmployeeListAsync(req, filter, 1);

            Assert.NotNull(_viewStore.LastAllowedBranchIds);
            Assert.Equal(activeBranches, _viewStore.LastAllowedBranchIds);
        }

        [Fact]
        public async Task List_OutOfRangeFilters_NormalizedToMinusOne()
        {
            var service = CreateService();
            _accessService.ScopeToReturn = new EmployeeAccessScope(99, true);

            var req = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };
            var filter = new InvoiceListFilter
            {
                TrangThaiPhatHanh = 9,
                TrangThaiThanhToan = 9
            };

            await service.GetEmployeeListAsync(req, filter, 99);

            Assert.NotNull(_viewStore.LastFilter);
            Assert.Equal(-1, _viewStore.LastFilter.TrangThaiPhatHanh);
            Assert.Equal(-1, _viewStore.LastFilter.TrangThaiThanhToan);
        }

        [Fact]
        public async Task List_ChuaThuDu_DiQuaNguyenVen_VaGiaTriLaVeMinusOne()
        {
            var service = CreateService();
            _accessService.ScopeToReturn = new EmployeeAccessScope(99, true);
            var req = new DataTableRequest { Draw = 1, Start = 0, Length = 10 };

            await service.GetEmployeeListAsync(req, new InvoiceListFilter { TrangThaiThanhToan = InvoiceListFilter.ChuaThuDu }, 99);
            Assert.Equal(-2, _viewStore.LastFilter.TrangThaiThanhToan);

            await service.GetEmployeeListAsync(req, new InvoiceListFilter { TrangThaiThanhToan = -3 }, 99);
            Assert.Equal(-1, _viewStore.LastFilter.TrangThaiThanhToan);

            foreach (var giaTri in new[] { -1, 0, 1, 2 })
            {
                await service.GetEmployeeListAsync(req, new InvoiceListFilter { TrangThaiThanhToan = giaTri }, 99);
                Assert.Equal(giaTri, _viewStore.LastFilter.TrangThaiThanhToan);
            }
        }

        [Fact]
        public async Task EmployeeDetail_OtherBranch_Null()
        {
            var service = CreateService();
            int hoaDonId = 100;
            _viewStore.BranchIds[hoaDonId] = 5;
            // Staff does not have InvoiceRead on branch 5

            var result = await service.GetEmployeeDetailAsync(hoaDonId, actorId: 2);

            Assert.Null(result);
        }

        [Fact]
        public async Task EmployeeDetail_Cancelled_ReturnedWithHistory()
        {
            var service = CreateService();
            int hoaDonId = 100;
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));

            var detail = new HoaDonChiTietRes
            {
                HoaDonId = hoaDonId,
                DaHuy = true,
                LyDoHuy = "Huy test"
            };
            _viewStore.Details[hoaDonId] = detail;

            var result = await service.GetEmployeeDetailAsync(hoaDonId, actorId: 2);

            Assert.NotNull(result);
            Assert.True(_viewStore.LastIncludeHistory);
            Assert.Equal(hoaDonId, _viewStore.LastDetailId);
        }

        [Fact]
        public async Task TenantDetail_NotOwner_Null_StoreNotCalled()
        {
            var service = CreateService();
            int hoaDonId = 100;
            int tenantId = 5;
            // _hoaDonStore.OwnedInvoices does not contain (100, 5)

            var result = await service.GetTenantDetailAsync(hoaDonId, tenantId);

            Assert.Null(result);
            Assert.Equal(0, _viewStore.GetDetailCallCount);
        }

        [Fact]
        public async Task TenantDetail_Owner_NoHistoryRequested()
        {
            var service = CreateService();
            int hoaDonId = 100;
            int tenantId = 5;
            _hoaDonStore.OwnedInvoices.Add((hoaDonId, tenantId));

            var detail = new HoaDonChiTietRes { HoaDonId = hoaDonId };
            _viewStore.Details[hoaDonId] = detail;

            var result = await service.GetTenantDetailAsync(hoaDonId, tenantId);

            Assert.NotNull(result);
            Assert.False(_viewStore.LastIncludeHistory);
        }

        [Fact]
        public async Task EmployeeDetail_ChangedBaseline_AddsReminderAndPeriodPrices_TenantDoesNot()
        {
            var service = CreateService();
            int hoaDonId = 100;
            int tenantId = 5;
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));
            _hoaDonStore.OwnedInvoices.Add((hoaDonId, tenantId));
            _viewStore.Details[hoaDonId] = new HoaDonChiTietRes { HoaDonId = hoaDonId };
            _viewStore.BaselineFacts[hoaDonId] = new InvoiceMeterBaselineFacts(
                ChiSoDienCu: 0m, ChiSoNuocCu: 50m, MoiDienKyTruoc: 4520m, MoiNuocKyTruoc: 50m,
                GhiChuDien: "[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 0; không có số cuối đồng hồ cũ; lý do: Thay", GhiChuNuoc: null,
                DonGiaDien: 3500m, DonGiaNuoc: 20000m);

            var staff = await service.GetEmployeeDetailAsync(hoaDonId, actorId: 2);
            // Fake trả đúng đối tượng đã đăng ký; cấp bản mới để thấy tenant không bị điền.
            _viewStore.Details[hoaDonId] = new HoaDonChiTietRes { HoaDonId = hoaDonId };
            var tenant = await service.GetTenantDetailAsync(hoaDonId, tenantId);

            Assert.NotNull(staff);
            var nhac = Assert.Single(staff!.NhacTruyThus);
            Assert.StartsWith("Kỳ này có đổi đồng hồ điện: [DOI_DONG_HO]", nhac);
            Assert.EndsWith("Kiểm tra đã thêm dòng truy thu nếu cần thu.", nhac);
            Assert.Equal(3500m, staff.DonGiaDienKy);
            Assert.Equal(20000m, staff.DonGiaNuocKy);

            Assert.NotNull(tenant);
            Assert.Empty(tenant!.NhacTruyThus);
            Assert.Null(tenant.DonGiaDienKy);
            Assert.Null(tenant.DonGiaNuocKy);
        }

        [Fact]
        public async Task EmployeeDetail_NoBaselineChange_HasNoReminder()
        {
            var service = CreateService();
            int hoaDonId = 100;
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));
            _viewStore.Details[hoaDonId] = new HoaDonChiTietRes { HoaDonId = hoaDonId };
            _viewStore.BaselineFacts[hoaDonId] = new InvoiceMeterBaselineFacts(100m, 50m, 100m, 50m, null, null, 3500m, 20000m);

            var staff = await service.GetEmployeeDetailAsync(hoaDonId, actorId: 2);

            Assert.Empty(staff!.NhacTruyThus);
            Assert.Equal(3500m, staff.DonGiaDienKy);
        }

        private const string TheDoiDongHoDien = "[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 0; không có số cuối đồng hồ cũ; lý do: Thay";

        private HoaDonChiTietRes SeedEmployeeInvoice(int hoaDonId, InvoiceMeterBaselineFacts? facts)
        {
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));
            _hoaDonStore.OwnedInvoices.Add((hoaDonId, 5));
            var detail = new HoaDonChiTietRes { HoaDonId = hoaDonId };
            _viewStore.Details[hoaDonId] = detail;
            if (facts != null) _viewStore.BaselineFacts[hoaDonId] = facts;
            return detail;
        }

        [Fact]
        public async Task TenantDetail_NeverFillsReminderOrPeriodPrices_EvenWhenPeriodChangedBaseline()
        {
            var service = CreateService();
            var detail = SeedEmployeeInvoice(100, new InvoiceMeterBaselineFacts(0m, 50m, 4520m, 50m, TheDoiDongHoDien, null, 3500m, 20000m));

            // Cùng một đối tượng chi tiết, chỉ gọi đường phía khách: nếu service điền thì sẽ thấy ngay trên đối tượng này.
            var tenant = await service.GetTenantDetailAsync(100, 5);

            Assert.Same(detail, tenant);
            Assert.Empty(tenant!.NhacTruyThus);
            Assert.Null(tenant.DonGiaDienKy);
            Assert.Null(tenant.DonGiaNuocKy);
        }

        [Fact]
        public async Task EmployeeDetail_WaterOnlyChanged_HasOnlyWaterReminderWithGenericSentenceWhenNoteIsGone()
        {
            var service = CreateService();
            SeedEmployeeInvoice(100, new InvoiceMeterBaselineFacts(100m, 0m, 100m, 9990m, null, "[Sửa 5 → 7] Nhập nhầm", 3500m, 20000m));

            var staff = await service.GetEmployeeDetailAsync(100, actorId: 2);

            var nhac = Assert.Single(staff!.NhacTruyThus);
            Assert.Equal("Kỳ này có đổi đồng hồ hoặc quay vòng nước (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu.", nhac);
        }

        [Fact]
        public async Task EmployeeDetail_BothMetersChanged_ListsElectricThenWater()
        {
            var service = CreateService();
            SeedEmployeeInvoice(100, new InvoiceMeterBaselineFacts(
                0m, 0m, 4520m, 9990m, TheDoiDongHoDien, "[QUAY_VONG] Chỉ số cũ 9.990; trần 10.000; phần trước mốc 10 m³; lý do: Quay vòng", 3500m, 20000m));

            var staff = await service.GetEmployeeDetailAsync(100, actorId: 2);

            Assert.Equal(2, staff!.NhacTruyThus.Count);
            Assert.StartsWith("Kỳ này có đổi đồng hồ điện: [DOI_DONG_HO]", staff.NhacTruyThus[0]);
            Assert.StartsWith("Kỳ này có quay vòng nước: [QUAY_VONG]", staff.NhacTruyThus[1]);
        }

        [Fact]
        public async Task EmployeeDetail_FirstEverPeriod_HasNoReminderButStillOffersPeriodPrices()
        {
            // B4: kỳ đầu tiên không có kỳ trước nên không có nhắc, nhưng nút truy thu vẫn cần đơn giá kỳ.
            var service = CreateService();
            SeedEmployeeInvoice(100, new InvoiceMeterBaselineFacts(0m, 0m, null, null, TheDoiDongHoDien, null, 3500m, 20000m));

            var staff = await service.GetEmployeeDetailAsync(100, actorId: 2);

            Assert.Empty(staff!.NhacTruyThus);
            Assert.Equal(3500m, staff.DonGiaDienKy);
            Assert.Equal(20000m, staff.DonGiaNuocKy);
        }

        [Fact]
        public async Task EmployeeDetail_InvoiceWithoutMeterPeriod_HasNoReminderAndNoPeriodPrices()
        {
            var service = CreateService();
            SeedEmployeeInvoice(100, facts: null);

            var staff = await service.GetEmployeeDetailAsync(100, actorId: 2);

            Assert.NotNull(staff);
            Assert.Empty(staff!.NhacTruyThus);
            Assert.Null(staff.DonGiaDienKy);
            Assert.Null(staff.DonGiaNuocKy);
        }

        [Fact]
        public async Task EmployeeDetail_WithoutInvoiceReadPermission_ReturnsNullAndNoReminder()
        {
            var service = CreateService();
            var detail = SeedEmployeeInvoice(100, new InvoiceMeterBaselineFacts(0m, 50m, 4520m, 50m, TheDoiDongHoDien, null, 3500m, 20000m));

            var other = await service.GetEmployeeDetailAsync(100, actorId: 99);

            Assert.Null(other);
            Assert.Empty(detail.NhacTruyThus);
        }

        [Fact]
        public async Task ExportEmployeePdf_Cancelled_NoQr()
        {
            var service = CreateService();
            int hoaDonId = 100;
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));

            var detail = new HoaDonChiTietRes
            {
                HoaDonId = hoaDonId,
                DaHuy = true,
                TrangThaiHoaDon = "Chưa thanh toán"
            };
            _viewStore.Details[hoaDonId] = detail;

            var bytes = await service.ExportEmployeePdfAsync(hoaDonId, actorId: 2);

            Assert.NotNull(bytes);
            Assert.Null(_exporter.LastQrBytes);
            Assert.NotNull(_exporter.LastHoaDon);
            Assert.True(_exporter.LastHoaDon.DaHuy);
        }

        [Fact]
        public async Task ExportEmployeePdf_DaGuiUnpaid_HasQr()
        {
            var service = CreateService();
            int hoaDonId = 100;
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));

            var detail = new HoaDonChiTietRes
            {
                HoaDonId = hoaDonId,
                DaHuy = false,
                TongTien = 1_500_000m,
                ConLai = 1_500_000m,
                TrangThaiHoaDon = "Chưa thanh toán"
            };
            _viewStore.Details[hoaDonId] = detail;

            var bytes = await service.ExportEmployeePdfAsync(hoaDonId, actorId: 2);

            Assert.NotNull(bytes);
            Assert.NotNull(_exporter.LastQrBytes);
        }

        [Fact]
        public async Task ExportEmployeePdf_FullyPaid_NoQr()
        {
            var service = CreateService();
            int hoaDonId = 101;
            _viewStore.BranchIds[hoaDonId] = 5;
            _accessService.Permissions.Add((2, 5, EmployeeActionCodes.InvoiceRead));
            _viewStore.Details[hoaDonId] = new HoaDonChiTietRes
            {
                HoaDonId = hoaDonId,
                TongTien = 1_500_000m,
                DaThu = 1_500_000m,
                ConLai = 0m,
                TrangThaiHoaDon = "Đã thanh toán"
            };

            var bytes = await service.ExportEmployeePdfAsync(hoaDonId, actorId: 2);

            Assert.NotNull(bytes);
            Assert.Null(_exporter.LastQrBytes);
        }

        [Fact]
        public async Task ExportTenantPdf_NotOwner_Null()
        {
            var service = CreateService();
            int hoaDonId = 100;
            int tenantId = 5;

            var bytes = await service.ExportTenantPdfAsync(hoaDonId, tenantId);

            Assert.Null(bytes);
        }

        [Fact]
        public async Task ExportTenantPdf_ChoDuyetKhongThuocQuyenXem_ReturnsNull()
        {
            // Hóa đơn ChoDuyet không qua được kiểm tra quyền xem của khách (store loại), nên không xuất PDF.
            var service = CreateService();
            const int hoaDonId = 110;
            const int tenantId = 5;
            _viewStore.Details[hoaDonId] = new HoaDonChiTietRes
            {
                HoaDonId = hoaDonId,
                TongTien = 1_000_000m,
                ConLai = 1_000_000m,
                TrangThaiPhatHanhValue = (int)TrangThaiPhatHanhHoaDon.ChoDuyet
            };

            var bytes = await service.ExportTenantPdfAsync(hoaDonId, tenantId);

            Assert.Null(bytes);
            Assert.Null(_exporter.LastHoaDon);
        }

        [Fact]
        public async Task ExportTenantPdf_DaGui_StillReturnsPdf()
        {
            var service = CreateService();
            const int hoaDonId = 111;
            const int tenantId = 5;
            _hoaDonStore.OwnedInvoices.Add((hoaDonId, tenantId));
            _viewStore.Details[hoaDonId] = new HoaDonChiTietRes
            {
                HoaDonId = hoaDonId,
                TongTien = 1_000_000m,
                ConLai = 1_000_000m,
                TrangThaiPhatHanhValue = (int)TrangThaiPhatHanhHoaDon.DaGui
            };

            var bytes = await service.ExportTenantPdfAsync(hoaDonId, tenantId);

            Assert.NotNull(bytes);
        }
    }
}
