using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
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
    }
}
