using System;
using System.IO;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterImagePortalAutoInvoiceTriggerTests
    {
        private static readonly DateTime FixedNowUtc = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        private const int RoomId = 101;
        private const int BranchId = 1;

        private readonly FakeMeterImageQueryStore _queryStore = new();
        private readonly FakeMeterImageStore _meterStore = new();
        private readonly FakeMeterReadingWorkflowService _workflow = new();
        private readonly FakeEmployeeAccessService _access = new();
        private readonly FakeAutoInvoiceService _autoInvoice = new();
        private readonly MeterImagePortalService _service;

        public MeterImagePortalAutoInvoiceTriggerTests()
        {
            _meterStore.RoomBranches[RoomId] = BranchId;
            _service = new MeterImagePortalService(
                _queryStore,
                _meterStore,
                _workflow,
                _access,
                new MeterImageOptions { StaleProcessingMinutes = 10 },
                new FixedTimeProvider(FixedNowUtc),
                _autoInvoice);
        }

        private static MeterImageUploadCommand UploadCommand() => new()
        {
            PhongTroId = RoomId,
            Thang = 10,
            Nam = 2026,
            LoaiDongHo = AppLoaiDongHo.Dien,
            File = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2)
        };

        private static MeterImageWorkflowResult WorkflowData() => new()
        {
            AnhChiSoDongHoId = 50,
            DichVuDienNuocCuaPhongId = 20,
            PhongTroId = RoomId,
            Thang = 10,
            Nam = 2026,
            LoaiDongHo = LoaiDongHo.Dien,
            TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
            NgayGui = FixedNowUtc
        };

        [Fact]
        public async Task Upload_ByTenant_TriggersKhachTaiAnh()
        {
            _workflow.OnUploadImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(WorkflowData(), "Tải ảnh thành công.");

            var result = await _service.UploadAsync(UploadCommand(), 5);

            Assert.True(result.Success);
            Assert.Equal("Tải ảnh thành công.", result.Message);
            var call = Assert.Single(_autoInvoice.Calls);
            Assert.Equal((5, AutoInvoiceTrigger.KhachTaiAnh, RoomId, 10, 2026), call);
        }

        [Fact]
        public async Task Upload_ByStaff_TriggersNhanVienTaiAnh()
        {
            _access.Permissions.Add((9, BranchId, EmployeeActionCodes.MeterUpload));
            _workflow.OnUploadImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(WorkflowData(), "Tải ảnh thành công.");

            var result = await _service.UploadAsync(UploadCommand(), 9);

            Assert.True(result.Success);
            var call = Assert.Single(_autoInvoice.Calls);
            Assert.Equal((9, AutoInvoiceTrigger.NhanVienTaiAnh, RoomId, 10, 2026), call);
        }

        [Fact]
        public async Task Upload_WhenWorkflowFails_DoesNotTrigger()
        {
            _workflow.OnUploadImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ đã chốt.");

            var result = await _service.UploadAsync(UploadCommand(), 5);

            Assert.False(result.Success);
            Assert.Equal("Kỳ đã chốt.", result.Message);
            Assert.Empty(_autoInvoice.Calls);
        }

        [Fact]
        public async Task Confirm_Success_TriggersXacNhanAnh_WithPeriodFromWorkflowResult()
        {
            _workflow.OnConfirmImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(WorkflowData(), "Xác nhận thành công.");

            var result = await _service.ConfirmAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 50, GiaTriXacNhan = 150m }, 9);

            Assert.True(result.Success);
            Assert.Equal("Xác nhận thành công.", result.Message);
            Assert.Equal((9, AutoInvoiceTrigger.XacNhanAnh, RoomId, 10, 2026), Assert.Single(_autoInvoice.Calls));
        }

        [Fact]
        public async Task Confirm_Failure_DoesNotTrigger()
        {
            _workflow.OnConfirmImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Fail("Không thể xác nhận.");

            var result = await _service.ConfirmAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 50, GiaTriXacNhan = 150m }, 9);

            Assert.False(result.Success);
            Assert.Empty(_autoInvoice.Calls);
        }

        [Fact]
        public async Task Correct_Success_TriggersSuaSoDaXacNhan()
        {
            _workflow.OnCorrectConfirmedImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(WorkflowData(), "Điều chỉnh thành công.");

            var result = await _service.CorrectAsync(new CorrectConfirmedMeterImageRequest(50, 155m, "Sửa số sai"), 9);

            Assert.True(result.Success);
            Assert.Equal("Điều chỉnh thành công.", result.Message);
            Assert.Equal((9, AutoInvoiceTrigger.SuaSoDaXacNhan, RoomId, 10, 2026), Assert.Single(_autoInvoice.Calls));
        }

        [Fact]
        public async Task Correct_Failure_DoesNotTrigger()
        {
            _workflow.OnCorrectConfirmedImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Fail("Không thể sửa.");

            var result = await _service.CorrectAsync(new CorrectConfirmedMeterImageRequest(50, 155m, "Sửa số sai"), 9);

            Assert.False(result.Success);
            Assert.Empty(_autoInvoice.Calls);
        }

        [Fact]
        public async Task RetryOcr_DoesNotTrigger()
        {
            _workflow.OnRetryOcr = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(WorkflowData(), "Thử lại thành công.");

            var result = await _service.RetryOcrAsync(50, 9);

            Assert.True(result.Success);
            Assert.Empty(_autoInvoice.Calls);
        }

        [Fact]
        public async Task WithoutAutoInvoiceService_OperationsStillWork()
        {
            var service = new MeterImagePortalService(
                _queryStore, _meterStore, _workflow, _access,
                new MeterImageOptions { StaleProcessingMinutes = 10 }, new FixedTimeProvider(FixedNowUtc));
            _workflow.OnConfirmImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(WorkflowData(), "Xác nhận thành công.");

            var result = await service.ConfirmAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 50, GiaTriXacNhan = 150m }, 9);

            Assert.True(result.Success);
            Assert.Empty(_autoInvoice.Calls);
        }
    }
}
