using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterImagePortalServiceTests
    {
        private readonly FakeMeterImageQueryStore _queryStore;
        private readonly FakeMeterImageStore _meterStore;
        private readonly FakeMeterReadingWorkflowService _workflowService;
        private readonly FakeEmployeeAccessService _accessService;
        private readonly MeterImageOptions _options;
        private readonly FixedTimeProvider _timeProvider;
        private readonly MeterImagePortalService _service;

        // Current fixed test time: 2026-10-15 10:00:00 UTC (17:00 VN)
        private static readonly DateTime FixedNowUtc = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

        public MeterImagePortalServiceTests()
        {
            _queryStore = new FakeMeterImageQueryStore();
            _meterStore = new FakeMeterImageStore();
            _workflowService = new FakeMeterReadingWorkflowService();
            _accessService = new FakeEmployeeAccessService();
            _options = new MeterImageOptions { StaleProcessingMinutes = 10 };
            _timeProvider = new FixedTimeProvider(FixedNowUtc);

            _service = new MeterImagePortalService(
                _queryStore,
                _meterStore,
                _workflowService,
                _accessService,
                _options,
                _timeProvider);
        }

        #region Tenant Query Tests

        [Theory]
        [InlineData(0, 10, 2026)]
        [InlineData(1, 0, 2026)]
        [InlineData(1, 13, 2026)]
        [InlineData(1, 10, 1999)]
        public async Task GetTenantRooms_InvalidParams_ReturnsFail(int actorId, int thang, int nam)
        {
            var result = await _service.GetTenantRoomsAsync(actorId, thang, nam);
            Assert.False(result.Success);
            Assert.Equal("Tham số không hợp lệ.", result.Message);
        }

        [Fact]
        public async Task GetTenantRooms_FuturePeriod_ReturnsFail()
        {
            var result = await _service.GetTenantRoomsAsync(1, 11, 2026);
            Assert.False(result.Success);
            Assert.Equal("Không thể xem kỳ chưa tới.", result.Message);
        }

        [Fact]
        public async Task GetTenantRooms_Valid_ReturnsRoomsFromQueryStore()
        {
            _queryStore.Rooms.Add(new MeterRoomRef(101, "P101", 1));
            var result = await _service.GetTenantRoomsAsync(1, 10, 2026);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal(101, result.Data[0].PhongTroId);
            Assert.Equal("P101", result.Data[0].TenPhong);
        }

        [Theory]
        [InlineData(0, 101, 10, 2026)]
        [InlineData(1, 101, 0, 2026)]
        [InlineData(1, 101, 13, 2026)]
        [InlineData(1, 101, 10, 1999)]
        public async Task GetTenantPeriod_InvalidParams_ReturnsFail(int actorId, int roomId, int thang, int nam)
        {
            var result = await _service.GetTenantPeriodAsync(actorId, roomId, thang, nam);
            Assert.False(result.Success);
            Assert.Equal("Tham số không hợp lệ.", result.Message);
        }

        [Fact]
        public async Task GetTenantPeriod_FuturePeriod_ReturnsFail()
        {
            var result = await _service.GetTenantPeriodAsync(1, 101, 11, 2026);
            Assert.False(result.Success);
            Assert.Equal("Không thể xem kỳ chưa tới.", result.Message);
        }

        [Fact]
        public async Task GetTenantPeriod_NoActiveContract_ReturnsFail()
        {
            var result = await _service.GetTenantPeriodAsync(1, 101, 10, 2026);
            Assert.False(result.Success);
            Assert.Equal("Bạn không có quyền xem ảnh chỉ số của phòng này.", result.Message);
        }

        [Fact]
        public async Task GetTenantPeriod_WithActiveContract_HidesStaffFields_AndPermitsUpload()
        {
            const int tenantId = 5;
            const int roomId = 101;
            const int thang = 10;
            const int nam = 2026;

            _meterStore.ActiveTenantContracts.Add((roomId, tenantId, thang, nam));
            _meterStore.ContractMonths.Add((roomId, thang, nam));
            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", 1);

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.Nhap, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            _queryStore.Images[20] = new List<MeterImageSnapshot>
            {
                new MeterImageSnapshot(1, LoaiDongHo.Dien, "http://img/1.jpg", TrangThaiXuLyAnhChiSo.DocDuoc,
                    110m, 0.95, 110m, true, FixedNowUtc, FixedNowUtc, "Lỗi giả định", "Ghi chú bảo mật", false)
            };

            var result = await _service.GetTenantPeriodAsync(tenantId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(roomId, result.Data.PhongTroId);
            Assert.Equal("P101", result.Data.TenPhong);
            Assert.True(result.Data.CoTheTaiAnh);
            Assert.Null(result.Data.LyDoKhongTheTai);

            Assert.Single(result.Data.AnhDien);
            var img = result.Data.AnhDien[0];
            Assert.Equal("http://img/1.jpg", img.Url);
            Assert.Equal(110m, img.GiaTriAIGoiY);
            Assert.Equal(110m, img.GiaTriXacNhan);
            Assert.True(img.LaChinhThuc);

            // Staff fields MUST be null/false for tenant
            Assert.Null(img.DoTinCay);
            Assert.Null(img.ThongBaoLoi);
            Assert.Null(img.GhiChuXacNhan);
            Assert.False(img.NguoiGuiLaKhachThue);
            Assert.False(img.CoTheThuLai);
            Assert.False(img.CoTheXacNhan);
            Assert.False(img.CanLyDoKhiXacNhan);
            Assert.False(img.CoTheSuaSo);
        }

        [Fact]
        public async Task GetTenantPeriod_TwoMonthsAgo_CoTheTaiAnhIsFalse_Rule2Reason()
        {
            const int tenantId = 5;
            const int roomId = 101;
            const int thang = 8;
            const int nam = 2026;

            _meterStore.ActiveTenantContracts.Add((roomId, tenantId, thang, nam));
            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", 1);

            var result = await _service.GetTenantPeriodAsync(tenantId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.False(result.Data.CoTheTaiAnh);
            Assert.Equal(MeterUploadRules.ReasonRule2TenantWindow, result.Data.LyDoKhongTheTai);
        }

        [Fact]
        public async Task GetTenantPeriod_ApprovedPeriod_CoTheTaiAnhIsFalse_Rule5Reason()
        {
            const int tenantId = 5;
            const int roomId = 101;
            const int thang = 10;
            const int nam = 2026;

            _meterStore.ActiveTenantContracts.Add((roomId, tenantId, thang, nam));
            _meterStore.ContractMonths.Add((roomId, thang, nam));
            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", 1);

            // Period is already approved (DaDuyet)
            var p = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 20,
                PhongTroId = roomId,
                Thang = thang,
                Nam = nam,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _meterStore.Periods[20] = p;
            _queryStore.Periods[(roomId, thang, nam)] = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.DaDuyet, 100, 110, 50, 55);

            var result = await _service.GetTenantPeriodAsync(tenantId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.False(result.Data.CoTheTaiAnh);
            Assert.Equal(MeterUploadRules.ReasonRule5PeriodApproved, result.Data.LyDoKhongTheTai);
        }

        [Fact]
        public async Task GetTenantPeriod_PreviousPeriodNotApproved_CoTheTaiAnhIsFalse_Rule6Reason()
        {
            const int tenantId = 5;
            const int roomId = 101;
            const int thang = 10;
            const int nam = 2026;

            _meterStore.ActiveTenantContracts.Add((roomId, tenantId, thang, nam));
            _meterStore.ContractMonths.Add((roomId, thang, nam));
            // Previous month had contract, but period is Nhap (not approved)
            _meterStore.ContractMonths.Add((roomId, 9, 2026));
            _meterStore.Periods[19] = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 19,
                PhongTroId = roomId,
                Thang = 9,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", 1);

            var result = await _service.GetTenantPeriodAsync(tenantId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.False(result.Data.CoTheTaiAnh);
            Assert.Equal(MeterUploadRules.FormatPreviousPeriodNotApprovedReason(9, 2026, 10, 2026), result.Data.LyDoKhongTheTai);
        }

        #endregion

        #region Staff Query Tests

        [Fact]
        public async Task GetStaffPeriod_RoomNotFound_ReturnsFail()
        {
            var result = await _service.GetStaffPeriodAsync(1, 999, 10, 2026);
            Assert.False(result.Success);
            Assert.Equal("Phòng trọ không tồn tại hoặc đã bị xóa.", result.Message);
        }

        [Fact]
        public async Task GetStaffPeriod_NoMeterReadPermission_ReturnsFail()
        {
            const int roomId = 101;
            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", 1);

            // Actor does not have MeterRead permission in branch 1
            var result = await _service.GetStaffPeriodAsync(1, roomId, 10, 2026);
            Assert.False(result.Success);
            Assert.Equal("Bạn không có quyền xem ảnh chỉ số của chi nhánh này.", result.Message);
        }

        [Fact]
        public async Task GetStaffPeriod_Valid_ReturnsStaffFieldsAndDoTinCay()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterReview));

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.Nhap, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            _queryStore.Images[20] = new List<MeterImageSnapshot>
            {
                new MeterImageSnapshot(1, LoaiDongHo.Dien, "http://img/1.jpg", TrangThaiXuLyAnhChiSo.DocDuoc,
                    110m, 0.95, 110m, false, FixedNowUtc, FixedNowUtc, "ThongBao", "GhiChu", true)
            };

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data.AnhDien);

            var img = result.Data.AnhDien[0];
            Assert.Equal(0.95, img.DoTinCay);
            Assert.Equal("ThongBao", img.ThongBaoLoi);
            Assert.Equal("GhiChu", img.GhiChuXacNhan);
            Assert.True(img.NguoiGuiLaKhachThue);
            Assert.True(img.CoTheXacNhan);
            Assert.False(img.CanLyDoKhiXacNhan);
        }

        [Fact]
        public async Task GetStaffPeriod_SubsequentPeriodExists_IsLocked_FlagsAllFalse()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterReview));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterUpload));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRetryOcr));

            _meterStore.SubsequentPeriods.Add((roomId, thang, nam));

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.Nhap, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            _queryStore.Images[20] = new List<MeterImageSnapshot>
            {
                new MeterImageSnapshot(1, LoaiDongHo.Dien, "http://img/1.jpg", TrangThaiXuLyAnhChiSo.DocDuoc,
                    110m, 0.95, 110m, false, FixedNowUtc, FixedNowUtc, null, null, false)
            };

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.True(result.Data.KyBiKhoa);
            Assert.Equal(MeterUploadRules.ReasonRule3SubsequentPeriod, result.Data.LyDoKhoa);
            Assert.False(result.Data.CoTheTaiAnh);

            var img = result.Data.AnhDien[0];
            Assert.False(img.CoTheThuLai);
            Assert.False(img.CoTheXacNhan);
            Assert.False(img.CanLyDoKhiXacNhan);
            Assert.False(img.CoTheSuaSo);
        }

        [Fact]
        public async Task GetStaffPeriod_NonDraftInvoiceExists_IsLocked_FlagsAllFalse()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));

            _meterStore.NonDraftInvoicePeriodIds.Add(20);

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.DaDuyet, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.True(result.Data.KyBiKhoa);
            Assert.Equal(MeterUploadRules.ReasonRule4LockedInvoice, result.Data.LyDoKhoa);
        }

        [Fact]
        public async Task GetStaffPeriod_ActionFlags_CoTheThuLai_FreshVsStaleProcessing()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRetryOcr));

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.Nhap, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            // Stale threshold is 10 minutes. FixedNowUtc = 10:00:00
            // Fresh: 09:55:00 (5 minutes ago -> <= 10 min)
            // Stale: 09:40:00 (20 minutes ago -> > 10 min)
            _queryStore.Images[20] = new List<MeterImageSnapshot>
            {
                new MeterImageSnapshot(1, LoaiDongHo.Dien, "http://img/1.jpg", TrangThaiXuLyAnhChiSo.DangXuLy,
                    null, null, null, false, FixedNowUtc.AddMinutes(-5), FixedNowUtc.AddMinutes(-5), null, null, false),
                new MeterImageSnapshot(2, LoaiDongHo.Dien, "http://img/2.jpg", TrangThaiXuLyAnhChiSo.DangXuLy,
                    null, null, null, false, FixedNowUtc.AddMinutes(-20), FixedNowUtc.AddMinutes(-20), null, null, false),
                new MeterImageSnapshot(3, LoaiDongHo.Dien, "http://img/3.jpg", TrangThaiXuLyAnhChiSo.KhongDocDuoc,
                    null, null, null, false, FixedNowUtc, FixedNowUtc, null, null, false),
                new MeterImageSnapshot(4, LoaiDongHo.Dien, "http://img/4.jpg", TrangThaiXuLyAnhChiSo.KhongDocDuoc,
                    null, null, null, true, FixedNowUtc, FixedNowUtc, null, null, false) // official -> cannot retry
            };

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);

            var imgFresh = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 1);
            var imgStale = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 2);
            var imgKhongDoc = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 3);
            var imgOfficial = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 4);

            Assert.False(imgFresh.CoTheThuLai);
            Assert.True(imgStale.CoTheThuLai);
            Assert.True(imgKhongDoc.CoTheThuLai);
            Assert.False(imgOfficial.CoTheThuLai);
        }

        [Fact]
        public async Task GetStaffPeriod_ActionFlags_CoTheXacNhan_And_CanLyDoKhiXacNhan()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterReview));

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.Nhap, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            _queryStore.Images[20] = new List<MeterImageSnapshot>
            {
                new MeterImageSnapshot(1, LoaiDongHo.Dien, "http://img/1.jpg", TrangThaiXuLyAnhChiSo.DocDuoc,
                    110m, 0.95, null, false, FixedNowUtc, FixedNowUtc, null, null, false),
                new MeterImageSnapshot(2, LoaiDongHo.Dien, "http://img/2.jpg", TrangThaiXuLyAnhChiSo.KhongDocDuoc,
                    null, null, null, false, FixedNowUtc, FixedNowUtc, null, null, false),
                new MeterImageSnapshot(3, LoaiDongHo.Dien, "http://img/3.jpg", TrangThaiXuLyAnhChiSo.Loi,
                    null, null, null, false, FixedNowUtc, FixedNowUtc, "Loi", null, false),
                new MeterImageSnapshot(4, LoaiDongHo.Dien, "http://img/4.jpg", TrangThaiXuLyAnhChiSo.DaXacNhan,
                    110m, 0.95, 110m, true, FixedNowUtc, FixedNowUtc, null, null, false)
            };

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);

            var imgDocDuoc = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 1);
            var imgKhongDoc = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 2);
            var imgLoi = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 3);
            var imgDaXacNhan = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 4);

            Assert.True(imgDocDuoc.CoTheXacNhan);
            Assert.False(imgDocDuoc.CanLyDoKhiXacNhan);

            Assert.True(imgKhongDoc.CoTheXacNhan);
            Assert.True(imgKhongDoc.CanLyDoKhiXacNhan);

            Assert.True(imgLoi.CoTheXacNhan);
            Assert.True(imgLoi.CanLyDoKhiXacNhan);

            Assert.False(imgDaXacNhan.CoTheXacNhan);
            Assert.False(imgDaXacNhan.CanLyDoKhiXacNhan);
        }

        [Fact]
        public async Task GetStaffPeriod_ActionFlags_CoTheSuaSo_OnlyForOfficialConfirmedImage()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterReview));

            var periodSnapshot = new MeterPeriodSnapshot(20, TrangThaiGhiNhan.Nhap, 100, 110, 50, 55);
            _queryStore.Periods[(roomId, thang, nam)] = periodSnapshot;

            _queryStore.Images[20] = new List<MeterImageSnapshot>
            {
                new MeterImageSnapshot(1, LoaiDongHo.Dien, "http://img/1.jpg", TrangThaiXuLyAnhChiSo.DaXacNhan,
                    110m, 0.95, 110m, true, FixedNowUtc, FixedNowUtc, null, null, false),
                new MeterImageSnapshot(2, LoaiDongHo.Dien, "http://img/2.jpg", TrangThaiXuLyAnhChiSo.DaXacNhan,
                    105m, 0.90, 105m, false, FixedNowUtc, FixedNowUtc, null, null, false)
            };

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);

            var imgOfficial = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 1);
            var imgNotOfficial = result.Data.AnhDien.First(i => i.AnhChiSoDongHoId == 2);

            Assert.True(imgOfficial.CoTheSuaSo);
            Assert.False(imgNotOfficial.CoTheSuaSo);
        }

        [Fact]
        public async Task GetStaffPeriod_NoPeriodRecord_ReadsFromNearestPreviousReading()
        {
            const int actorId = 1;
            const int roomId = 101;
            const int branchId = 1;
            const int thang = 10;
            const int nam = 2026;

            _queryStore.RoomDetails[roomId] = new MeterRoomRef(roomId, "P101", branchId);
            _accessService.Permissions.Add((actorId, branchId, EmployeeActionCodes.MeterRead));

            _meterStore.DefaultChiSoDienMoi = 320m;
            _meterStore.DefaultChiSoNuocMoi = 85m;

            var result = await _service.GetStaffPeriodAsync(actorId, roomId, thang, nam);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.False(result.Data.CoBanGhiKy);
            Assert.Equal(320m, result.Data.ChiSoDienCu);
            Assert.Equal(320m, result.Data.ChiSoDienMoi);
            Assert.Equal(85m, result.Data.ChiSoNuocCu);
            Assert.Equal(85m, result.Data.ChiSoNuocMoi);
        }

        #endregion

        #region Workflow Forwarding and Command Tests

        [Fact]
        public async Task Upload_InvalidLoaiDongHo_ReturnsFail_WorkflowNotCalled()
        {
            var command = new MeterImageUploadCommand
            {
                PhongTroId = 101,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = (AppLoaiDongHo)99,
                File = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2)
            };

            bool workflowCalled = false;
            _workflowService.OnUploadImage = (_, _) =>
            {
                workflowCalled = true;
                return ServiceResult<MeterImageWorkflowResult>.Ok(new MeterImageWorkflowResult());
            };

            var result = await _service.UploadAsync(command, 1);

            Assert.False(result.Success);
            Assert.Equal("Loại đồng hồ không hợp lệ.", result.Message);
            Assert.False(workflowCalled);
        }

        [Fact]
        public async Task Upload_WorkflowResultForwarded_TenantModeIfNoMeterUploadPerm()
        {
            const int actorId = 5;
            const int roomId = 101;
            const int branchId = 1;

            _meterStore.RoomBranches[roomId] = branchId;
            // Actor does NOT have MeterUpload permission -> treated as tenant for response mapping

            var command = new MeterImageUploadCommand
            {
                PhongTroId = roomId,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = AppLoaiDongHo.Dien,
                File = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2)
            };

            _workflowService.OnUploadImage = (_, _) => ServiceResult<MeterImageWorkflowResult>.Ok(
                new MeterImageWorkflowResult
                {
                    AnhChiSoDongHoId = 10,
                    DichVuDienNuocCuaPhongId = 20,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "http://img/test.jpg",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                    GiaTriAIGoiY = 125m,
                    DoTinCay = 0.92,
                    GiaTriXacNhan = 125m,
                    DuocChonLamChiSoChinhThuc = false,
                    NgayGui = FixedNowUtc,
                    ThongBaoLoi = "Secret error"
                }, "Tải ảnh thành công.");

            var result = await _service.UploadAsync(command, actorId);

            Assert.True(result.Success);
            Assert.Equal("Tải ảnh thành công.", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(10, result.Data.AnhChiSoDongHoId);
            Assert.Equal(AppLoaiDongHo.Dien, result.Data.LoaiDongHo);
            Assert.Equal(AppTrangThaiAnhChiSo.DocDuoc, result.Data.TrangThai);
            Assert.Equal(125m, result.Data.GiaTriAIGoiY);

            // Tenant mapping clears staff fields
            Assert.Null(result.Data.DoTinCay);
            Assert.Null(result.Data.ThongBaoLoi);
        }

        [Fact]
        public async Task RetryOcr_ForwardedToWorkflow()
        {
            _workflowService.OnRetryOcr = (id, actor) =>
            {
                Assert.Equal(50, id);
                Assert.Equal(1, actor);
                return ServiceResult<MeterImageWorkflowResult>.Ok(new MeterImageWorkflowResult
                {
                    AnhChiSoDongHoId = 50,
                    LoaiDongHo = LoaiDongHo.Nuoc,
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
                }, "Thử lại thành công.");
            };

            var result = await _service.RetryOcrAsync(50, 1);
            Assert.True(result.Success);
            Assert.Equal("Thử lại thành công.", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(50, result.Data.AnhChiSoDongHoId);
            Assert.Equal(AppLoaiDongHo.Nuoc, result.Data.LoaiDongHo);
            Assert.Equal(AppTrangThaiAnhChiSo.DocDuoc, result.Data.TrangThai);
        }

        [Fact]
        public async Task Confirm_ForwardedToWorkflow()
        {
            var req = new ConfirmMeterImageRequest
            {
                AnhChiSoDongHoId = 50,
                GiaTriXacNhan = 150m,
                GhiChu = "Ok"
            };

            _workflowService.OnConfirmImage = (r, actor) =>
            {
                Assert.Equal(50, r.AnhChiSoDongHoId);
                Assert.Equal(150m, r.GiaTriXacNhan);
                return ServiceResult<MeterImageWorkflowResult>.Ok(new MeterImageWorkflowResult
                {
                    AnhChiSoDongHoId = 50,
                    LoaiDongHo = LoaiDongHo.Dien,
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                    GiaTriXacNhan = 150m,
                    DuocChonLamChiSoChinhThuc = true
                }, "Xác nhận thành công.");
            };

            var result = await _service.ConfirmAsync(req, 1);
            Assert.True(result.Success);
            Assert.Equal("Xác nhận thành công.", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(150m, result.Data.GiaTriXacNhan);
            Assert.True(result.Data.LaChinhThuc);
        }

        [Fact]
        public async Task Correct_ForwardedToWorkflow()
        {
            var req = new CorrectConfirmedMeterImageRequest(50, 155m, "Sửa số sai");

            _workflowService.OnCorrectConfirmedImage = (r, actor) =>
            {
                Assert.Equal(50, r.AnhChiSoDongHoId);
                Assert.Equal(155m, r.GiaTriMoi);
                return ServiceResult<MeterImageWorkflowResult>.Ok(new MeterImageWorkflowResult
                {
                    AnhChiSoDongHoId = 50,
                    LoaiDongHo = LoaiDongHo.Dien,
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                    GiaTriXacNhan = 155m,
                    DuocChonLamChiSoChinhThuc = true
                }, "Điều chỉnh thành công.");
            };

            var result = await _service.CorrectAsync(req, 1);
            Assert.True(result.Success);
            Assert.Equal("Điều chỉnh thành công.", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(155m, result.Data.GiaTriXacNhan);
        }

        #endregion

        #region DienNuocService Image Summary Tests

        [Fact]
        public async Task GetDanhSachDienNuoc_PopulatesImageSummary_WithOfficialImage()
        {
            var store = new FakeDienNuocStore();
            store.ActiveContracts.Add(new HopDong
            {
                HopDongId = 1,
                PhongTroId = 101,
                PhongTro = new PhongTro { PhongTroId = 101, SoPhong = "101", ChiNhanhId = 1, MoTa = "Test" },
                ThoiDiemBatDau = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                NguoiThue = new NguoiThue { NguoiThueId = 1, HoVaTen = "Nguyen Van A", CCCD = "012345678901" }
            });
            var access = new FakeEmployeeAccessService();
            access.Permissions.Add((1, 1, EmployeeActionCodes.MeterRead));

            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };

            // Electric: 2 images, one official (DaXacNhan)
            record.AnhChiSoDongHos.Add(new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                DuocChonLamChiSoChinhThuc = false,
                NgayGui = DateTime.UtcNow.AddMinutes(-10)
            });
            record.AnhChiSoDongHos.Add(new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 2,
                LoaiDongHo = LoaiDongHo.Dien,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                DuocChonLamChiSoChinhThuc = true,
                NgayGui = DateTime.UtcNow.AddMinutes(-5)
            });

            // Water: 1 image (MoiTaiLen)
            record.AnhChiSoDongHos.Add(new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 3,
                LoaiDongHo = LoaiDongHo.Nuoc,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.MoiTaiLen,
                DuocChonLamChiSoChinhThuc = false,
                NgayGui = DateTime.UtcNow
            });

            store.CurrentMonthRecords[101] = record;

            var dienNuocService = new DienNuocService(
                store,
                access,
                _workflowService);

            var list = await dienNuocService.GetDanhSachDienNuocAsync(1, 10, 2026, 1);

            Assert.NotNull(list);
            Assert.Single(list);

            var row = list[0];
            Assert.Equal(2, row.SoAnhDien);
            Assert.Equal(AppTrangThaiAnhChiSo.DaXacNhan, row.TrangThaiAnhDien); // picks official

            Assert.Equal(1, row.SoAnhNuoc);
            Assert.Equal(AppTrangThaiAnhChiSo.MoiTaiLen, row.TrangThaiAnhNuoc);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_WithoutOfficialImage_PicksLatest_AndExcludesDeleted()
        {
            var store = new FakeDienNuocStore();
            store.ActiveContracts.Add(new HopDong
            {
                HopDongId = 1,
                PhongTroId = 101,
                PhongTro = new PhongTro { PhongTroId = 101, SoPhong = "101", ChiNhanhId = 1, MoTa = "Test" },
                ThoiDiemBatDau = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                NguoiThue = new NguoiThue { NguoiThueId = 1, HoVaTen = "Nguyen Van A", CCCD = "012345678901" }
            });
            var access = new FakeEmployeeAccessService();
            access.Permissions.Add((1, 1, EmployeeActionCodes.MeterRead));

            var record = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 101,
                Thang = 10,
                Nam = 2026
            };

            // Older image: Loi
            record.AnhChiSoDongHos.Add(new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi,
                NgayGui = DateTime.UtcNow.AddMinutes(-20)
            });
            // Newer image: DocDuoc
            record.AnhChiSoDongHos.Add(new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 2,
                LoaiDongHo = LoaiDongHo.Dien,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                NgayGui = DateTime.UtcNow.AddMinutes(-5)
            });
            // Deleted image: should be ignored
            record.AnhChiSoDongHos.Add(new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 3,
                LoaiDongHo = LoaiDongHo.Dien,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                NgayGui = DateTime.UtcNow,
                IsDeleted = true
            });

            store.CurrentMonthRecords[101] = record;

            var dienNuocService = new DienNuocService(
                store,
                access,
                _workflowService);

            var list = await dienNuocService.GetDanhSachDienNuocAsync(1, 10, 2026, 1);

            Assert.NotNull(list);
            Assert.Single(list);

            var row = list[0];
            Assert.Equal(2, row.SoAnhDien); // deleted excluded
            Assert.Equal(AppTrangThaiAnhChiSo.DocDuoc, row.TrangThaiAnhDien); // latest non-deleted
            Assert.Equal(0, row.SoAnhNuoc);
            Assert.Null(row.TrangThaiAnhNuoc);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_NoPeriodRecord_ReturnsZeroAndNull()
        {
            var store = new FakeDienNuocStore();
            store.ActiveContracts.Add(new HopDong
            {
                HopDongId = 1,
                PhongTroId = 101,
                PhongTro = new PhongTro { PhongTroId = 101, SoPhong = "101", ChiNhanhId = 1, MoTa = "Test" },
                ThoiDiemBatDau = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                NguoiThue = new NguoiThue { NguoiThueId = 1, HoVaTen = "Nguyen Van A", CCCD = "012345678901" }
            });
            var access = new FakeEmployeeAccessService();
            access.Permissions.Add((1, 1, EmployeeActionCodes.MeterRead));

            // No current records in store for room 101

            var dienNuocService = new DienNuocService(
                store,
                access,
                _workflowService);

            var list = await dienNuocService.GetDanhSachDienNuocAsync(1, 10, 2026, 1);

            Assert.NotNull(list);
            Assert.Single(list);

            var row = list[0];
            Assert.Equal(0, row.SoAnhDien);
            Assert.Null(row.TrangThaiAnhDien);
            Assert.Equal(0, row.SoAnhNuoc);
            Assert.Null(row.TrangThaiAnhNuoc);
        }

        private FakeDienNuocStore StoreWithRoom101()
        {
            var store = new FakeDienNuocStore();
            store.ActiveContracts.Add(new HopDong
            {
                HopDongId = 1,
                PhongTroId = 101,
                PhongTro = new PhongTro { PhongTroId = 101, SoPhong = "101", ChiNhanhId = 1, MoTa = "Test" },
                ThoiDiemBatDau = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                NguoiThue = new NguoiThue { NguoiThueId = 1, HoVaTen = "Nguyen Van A", CCCD = "012345678901" }
            });
            return store;
        }

        private async Task<DienNuocPhongRes> GetRow101(FakeDienNuocStore store)
        {
            var access = new FakeEmployeeAccessService();
            access.Permissions.Add((1, 1, EmployeeActionCodes.MeterRead));
            var list = await new DienNuocService(store, access, _workflowService).GetDanhSachDienNuocAsync(1, 10, 2026, 1);
            return Assert.Single(list);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_PreviousMonthHadContractButNotApproved_RowLockedWithReason()
        {
            var store = StoreWithRoom101();
            store.ContractMonths.Add((101, 9, 2026));
            store.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { PhongTroId = 101, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap };

            var row = await GetRow101(store);

            Assert.True(row.IsLocked);
            Assert.Contains("09/2026", row.LyDoKhoa);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_PreviousMonthHadContractAndNoRecord_RowLocked()
        {
            var store = StoreWithRoom101();
            store.ContractMonths.Add((101, 9, 2026));

            var row = await GetRow101(store);

            Assert.True(row.IsLocked);
            Assert.False(string.IsNullOrWhiteSpace(row.LyDoKhoa));
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_PreviousMonthApproved_RowNotLocked()
        {
            var store = StoreWithRoom101();
            store.ContractMonths.Add((101, 9, 2026));
            store.PreviousMonthRecords[101] = new DichVuDienNuocCuaPhong { PhongTroId = 101, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet };

            var row = await GetRow101(store);

            Assert.False(row.IsLocked);
            Assert.Null(row.LyDoKhoa);
        }

        [Fact]
        public async Task GetDanhSachDienNuoc_RoomLockedByInvoiceOrLaterPeriod_HasReason()
        {
            var store = StoreWithRoom101();
            store.LockedRooms.Add(101);

            var row = await GetRow101(store);

            Assert.True(row.IsLocked);
            Assert.False(string.IsNullOrWhiteSpace(row.LyDoKhoa));
        }

        #endregion
    }
}
