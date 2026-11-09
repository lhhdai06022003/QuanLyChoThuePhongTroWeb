using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterReadingWorkflowTests
    {
        #region Fake Implementations

        private class FakeUnitOfWork : IUnitOfWork
        {
            public int SaveChangesCallCount { get; set; }
            public bool ThrowOnNextSave { get; set; }
            public int ThrowOnSaveCallIndex { get; set; } = -1;
            public FakeTransaction CurrentTransaction { get; } = new();

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (ThrowOnNextSave || SaveChangesCallCount == ThrowOnSaveCallIndex)
                {
                    ThrowOnNextSave = false;
                    throw new InvalidOperationException("Simulated Database Save Failure");
                }
                SaveChangesCallCount++;
                return Task.FromResult(1);
            }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IApplicationTransaction>(CurrentTransaction);
            }
        }

        private class FakeTransaction : IApplicationTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                Committed = true;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                RolledBack = true;
                return Task.CompletedTask;
            }

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeMeterImageStorageService : IMeterImageStorageService
        {
            public List<string> DeletedPublicIds { get; } = new();
            public byte[] ReadBytesToReturn { get; set; } = new byte[] { 1, 2, 3 };
            public string ReadContentTypeToReturn { get; set; } = "image/jpeg";
            public bool ThrowOnUpload { get; set; }
            public bool ThrowOnDelete { get; set; }
            public Action? OnUpload { get; set; }

            public int UploadCallCount { get; set; }
            public byte[]? UploadedBytes { get; private set; }
            public int ReadCallCount { get; private set; }

            public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
            {
                UploadCallCount++;
                if (ThrowOnUpload) throw new InvalidOperationException("Upload storage failed");
                OnUpload?.Invoke();
                using var copy = new MemoryStream();
                file.Content.CopyTo(copy);
                UploadedBytes = copy.ToArray();
                return Task.FromResult(new MeterImageUploadResult("https://storage.test/img1.jpg", "meters/img1"));
            }

            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
            {
                ReadCallCount++;
                return Task.FromResult(new MeterImageReadResult(ReadBytesToReturn, ReadContentTypeToReturn));
            }

            public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
            {
                if (ThrowOnDelete) throw new InvalidOperationException("Delete cleanup failed");
                DeletedPublicIds.Add(publicId);
                return Task.CompletedTask;
            }
        }

        private class FakeMeterOcrService : IMeterOcrService
        {
            public MeterOcrResult ResultToReturn { get; set; } = MeterOcrResult.Readable(150.5m, 0.95);
            public bool ThrowOnProcess { get; set; }
            public Action? OnProcess { get; set; }
            public byte[]? LastImageBytes { get; private set; }
            public string? LastContentType { get; private set; }

            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
            {
                LastImageBytes = imageBytes;
                LastContentType = contentType;
                if (ThrowOnProcess) throw new InvalidOperationException("AI OCR timeout");
                OnProcess?.Invoke();
                return Task.FromResult(ResultToReturn);
            }
        }

        private class FakeEmployeeAccessService : IEmployeeAccessService
        {
            public HashSet<(int ActorId, int BranchId, string Action)> Permissions { get; } = new();

            public Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Permissions.Contains((actorId, chiNhanhId, action)));
            }

            public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<EmployeeAccessScope?>(null);
            }
        }

        #endregion

        private readonly FakeMeterImageStore _store;
        private readonly FakeMeterImageStorageService _storage;
        private readonly FakeMeterOcrService _ocr;
        private readonly FakeEmployeeAccessService _access;
        private readonly FakeUnitOfWork _uow;
        private readonly Fakes.FakeHoaDonStore _hoaDonStore;
        private readonly QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService _calculator;
        private readonly Fakes.FixedTimeProvider _timeProvider;
        private readonly MeterReadingWorkflowService _service;
        private readonly Fakes.FakeMeterConsumptionHistoryStore _historyStore = new();

        public MeterReadingWorkflowTests()
        {
            _store = new FakeMeterImageStore();
            _storage = new FakeMeterImageStorageService();
            _ocr = new FakeMeterOcrService();
            _access = new FakeEmployeeAccessService();
            _uow = new FakeUnitOfWork();
            _hoaDonStore = new Fakes.FakeHoaDonStore();
            _calculator = new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService();
            _timeProvider = new Fakes.FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero));

            _service = new MeterReadingWorkflowService(
                _store,
                _storage,
                _ocr,
                _access,
                _uow,
                _hoaDonStore,
                _calculator,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<MeterReadingWorkflowService>.Instance,
                new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.MeterImageOptions(),
                _historyStore,
                new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoInvoiceOptions(),
                _timeProvider);
        }

        private static UploadMeterImageRequest CreateValidRequest(int phongTroId = 1)
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            return new UploadMeterImageRequest
            {
                PhongTroId = phongTroId,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = new UploadFile(stream, "meter.jpg", "image/jpeg", 3)
            };
        }

        [Fact]
        public async Task UploadImageAsync_WhenTenantHasNoActiveContract_ReturnsFail()
        {
            _store.RoomBranches[1] = 10;
            // Actor 99 không có hợp đồng và không có quyền nhân viên
            var req = CreateValidRequest();

            var result = await _service.UploadImageAsync(req, 99);

            Assert.False(result.Success);
            Assert.Contains("quyền", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task UploadImageAsync_WhenTenantHasActiveContract_UploadsAndRunsOcr_WithoutSettingConfirmedValue()
        {
            _store.RoomBranches[1] = 10;
            _store.ActiveTenantContracts.Add((1, 99, 10, 2026));
            _ocr.ResultToReturn = MeterOcrResult.Readable(150.5m, 0.95);

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 99);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(150.5m, result.Data.GiaTriAIGoiY);
            Assert.Null(result.Data.GiaTriXacNhan); // AI không tự xác nhận
            Assert.Equal(0.95, result.Data.DoTinCay);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, result.Data.TrangThaiXuLy);

            // Kiểm tra thứ tự: có ít nhất 2 lần save (1 trước OCR, 1 sau OCR)
            Assert.True(_uow.SaveChangesCallCount >= 2);
        }

        [Fact]
        public async Task UploadImageAsync_WhenDatabaseSaveFailsInitially_CleansUpOrphanFile()
        {
            _store.RoomBranches[1] = 10;
            _store.ActiveTenantContracts.Add((1, 99, 10, 2026));
            _uow.ThrowOnNextSave = true;

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 99);

            Assert.False(result.Success);
            Assert.Contains("meters/img1", _storage.DeletedPublicIds); // Cleanup file mồ côi
        }

        [Fact]
        public async Task UploadImageAsync_WhenPeriodHasSubsequentPeriod_ReturnsFail()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((1, 10, EmployeeActionCodes.MeterUpload));
            _store.SubsequentPeriods.Add((1, 10, 2026));

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 1);

            Assert.False(result.Success);
            Assert.Contains("kỳ sau", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task RetryOcrAsync_WhenTenantCalls_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi
            };
            _store.Images[100] = img;

            // Tenant (không có MeterRetryOcr)
            var result = await _service.RetryOcrAsync(100, 99);

            Assert.False(result.Success);
            Assert.Contains("quyền", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task RetryOcrAsync_WhenStaffAuthorized_UpdatesSameImage()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                PublicId = "p1",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
            };
            _store.Images[100] = img;

            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));
            _ocr.ResultToReturn = MeterOcrResult.Readable(180m, 0.9);

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(180m, result.Data.GiaTriAIGoiY);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, result.Data.TrangThaiXuLy);
            Assert.Equal(100, result.Data.AnhChiSoDongHoId); // Cập nhật cùng entity
        }

        [Fact]
        public async Task ConfirmImageAsync_WhenUnreadableAndMissingReason_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            // Thiếu GhiChu lý do
            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150m, GhiChu = "" };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("lý do", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task ConfirmImageAsync_WhenValueSmallerThanOldReading_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 90m };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("nhỏ hơn", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task ConfirmImageAsync_ReplacesOldOfficialImage_InSameTransaction()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 120
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var oldOfficial = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 99,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan
            };
            _store.Images[99] = oldOfficial;

            var newImage = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://new",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
            };
            _store.Images[100] = newImage;

            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150m };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.False(oldOfficial.DuocChonLamChiSoChinhThuc); // Ảnh cũ bị bỏ cờ
            Assert.True(newImage.DuocChonLamChiSoChinhThuc);     // Ảnh mới được chọn
            Assert.Equal(150m, period.ChiSoDienMoi);             // Số kỳ được cập nhật
            Assert.True(_uow.CurrentTransaction.Committed);
        }

        private void SeedAnomalyConfirm(out AnhChiSoDongHo img)
        {
            // Lịch sử điện 6 kỳ trung vị 40 kWh: tiêu thụ 91 kWh là bất thường (ngưỡng 90).
            _historyStore.History = new MeterConsumptionHistory(new[] { 38m, 40m, 40m, 42m, 45m, 39m }.Select(k => new MeterConsumption(k, 0m)).ToList(), 31, 31, 10);
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 1000 };
            _store.RoomBranches[1] = 10;
            img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));
        }

        [Fact]
        public async Task XacNhanSoBatThuong_KhongGhiChu_BiTuChoi()
        {
            SeedAnomalyConfirm(out var img);

            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 1091m }, 2);

            Assert.False(result.Success);
            Assert.StartsWith("Chỉ số bất thường so với lịch sử, cần ghi chú lý do. ", result.Message);
            Assert.Contains("Điện", result.Message);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, img.TrangThaiXuLy);
        }

        [Fact]
        public async Task XacNhanSoBatThuong_CoGhiChu_ThanhCong()
        {
            SeedAnomalyConfirm(out var img);

            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 1091m, GhiChu = "Khách dùng máy nóng lạnh mới" }, 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, img.TrangThaiXuLy);
        }

        [Fact]
        public async Task XacNhanSoBinhThuong_KhongCanGhiChu()
        {
            SeedAnomalyConfirm(out var img);

            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 1090m }, 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, img.TrangThaiXuLy);
        }

        [Theory]
        [InlineData(TrangThaiXuLyAnhChiSo.MoiTaiLen)]
        [InlineData(TrangThaiXuLyAnhChiSo.DangXuLy)]
        public async Task ConfirmImageAsync_WhenMoiTaiLenOrDangXuLy_ReturnsFailWithoutException(TrangThaiXuLyAnhChiSo state)
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = state
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150m };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("đang được nhận diện", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task ConfirmImageAsync_WhenDaXacNhan_ReturnsFailWithoutException()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150m };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("đã được xác nhận", result.Message.ToLowerInvariant());
        }

        [Theory]
        [InlineData(TrangThaiXuLyAnhChiSo.CanChupLai, false)]
        [InlineData(TrangThaiXuLyAnhChiSo.DaThayThe, false)]
        [InlineData(TrangThaiXuLyAnhChiSo.DocDuoc, true)]
        public async Task ConfirmImageAsync_WhenInvalidStateOrDeleted_ReturnsFailWithoutException(TrangThaiXuLyAnhChiSo state, bool isDeleted)
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = state,
                IsDeleted = isDeleted
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150m };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("không còn hiệu lực", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenOneEvidenceMissing_RollsBack_AndReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            // Chỉ có ảnh điện chính thức, nước chưa có bằng chứng
            var imgDien = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 1,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                GiaTriXacNhan = 150m
            };
            _store.Images[1] = imgDien;

            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.OfficialImage,
                        NuocMode = MeterReadingSubmissionMode.OfficialImage
                    }
                }
            };
            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("nước", result.Message.ToLowerInvariant());
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WithManualReadingsAndReason_Succeeds_AndSetsDaDuyet()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 160m,
                        LyDoDienThuCong = "Đồng hồ điện bị mờ số",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 65m,
                        LyDoNuocThuCong = "Khách không có nhà, ghi nhận trực tiếp"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(160m, period.ChiSoDienMoi);
            Assert.Equal(65m, period.ChiSoNuocMoi);
            Assert.Contains("Đồng hồ điện bị mờ số", period.GhiChuDuyet);
            Assert.Contains("Khách không có nhà", period.GhiChuDuyet);
            Assert.True(_uow.CurrentTransaction.Committed);
        }

        private static ApproveMeterPeriodsRequest ManualApproveRequestForRooms(int thang, int nam, params int[] phongTroIds) => new()
        {
            Thang = thang,
            Nam = nam,
            DanhSachPhong = phongTroIds.Select(id => new ApproveMeterPeriodItem
            {
                PhongTroId = id,
                DienMode = MeterReadingSubmissionMode.Manual,
                ChiSoDienMoiThuCong = 160m,
                LyDoDienThuCong = "Ghi trực tiếp",
                NuocMode = MeterReadingSubmissionMode.Manual,
                ChiSoNuocMoiThuCong = 65m,
                LyDoNuocThuCong = "Ghi trực tiếp"
            }).ToList()
        };

        // Tra chi nhánh của cả danh sách trong một lần (docs/features/toi-uu-hieu-nang, bước 6);
        // thông báo lỗi giữ như khi tra từng phòng theo id tăng dần: gặp lỗi nào trước thì báo lỗi đó.
        [Fact]
        public async Task ApprovePeriodsAsync_BranchesDiffer_BeforeMissingRoom_ReportsBranchMismatch()
        {
            _store.RoomBranches[1] = 10;
            _store.RoomBranches[2] = 20;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 3, 2, 1), 2);

            Assert.False(result.Success);
            Assert.Equal("Danh sách phòng không thuộc cùng một chi nhánh.", result.Message);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_MissingRoom_BeforeBranchMismatch_ReportsMissingRoom()
        {
            _store.RoomBranches[1] = 10;
            _store.RoomBranches[3] = 20;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 3, 2, 1), 2);

            Assert.False(result.Success);
            Assert.Equal("Phòng trọ 2 không tồn tại hoặc đã bị xóa.", result.Message);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_ManyRooms_LooksUpBranchesOnce()
        {
            for (var id = 1; id <= 3; id++)
            {
                _store.RoomBranches[id] = 10;
                _store.Periods[id] = new DichVuDienNuocCuaPhong
                {
                    DichVuDienNuocCuaPhongId = id, PhongTroId = id, Thang = 10, Nam = 2026,
                    ChiSoDienCu = 100, ChiSoNuocCu = 50, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
            }
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 1, 2, 3), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, _store.GetBranchIdsByRoomsCalls);
            Assert.Equal(0, _store.GetBranchIdByRoomCalls);
            Assert.All(_store.Periods.Values, p => Assert.Equal(TrangThaiGhiNhan.DaDuyet, p.TrangThaiGhiNhan));
        }

        // Bước 12 (docs/features/toi-uu-hieu-nang): dữ liệu kiểm tra của mọi phòng đọc một lượt,
        // nhưng vẫn xét lần lượt từng phòng theo id tăng dần như trước.
        [Fact]
        public async Task ApprovePeriodsAsync_FirstRoomFailsLate_SecondRoomFailsEarly_ReportsFirstRoom()
        {
            var (room1, _) = SetupTwoRoomsInOctober();
            room1.ChiSoDienCu = 200; // phòng 1 nhập tay 160 < 200: hỏng ở bước xét chỉ số
            _store.SubsequentPeriods.Add((2, 10, 2026)); // phòng 2 hỏng ngay bước kiểm kỳ sau

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 2, 1), 2);

            Assert.False(result.Success);
            Assert.Equal("Phòng 1: chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_SecondRoomHasLockedInvoice_ReportsSecondRoom_AndRollsBack()
        {
            var (_, room2) = SetupTwoRoomsInOctober();
            room2.ChiSoDienCu = 100;
            room2.ChiSoNuocCu = 50;
            _store.NonDraftInvoicePeriodIds.Add(2);

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 1, 2), 2);

            Assert.False(result.Success);
            Assert.Equal("Phòng 2 kỳ này đã có hóa đơn đã phát hành, không thể duyệt.", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, room2.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_ManualAndOfficialRooms_UseEachRoomsOwnImages()
        {
            var (room1, room2) = SetupTwoRoomsInOctober();
            void Image(int id, int periodId, LoaiDongHo loai, bool official, decimal value) => _store.Images[id] = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = id,
                DichVuDienNuocCuaPhongId = periodId,
                LoaiDongHo = loai,
                DuocChonLamChiSoChinhThuc = official,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = value
            };
            Image(101, 1, LoaiDongHo.Dien, official: true, 150m);   // phòng 1 nhập tay: ảnh chính thức cũ bị bỏ chọn
            Image(102, 2, LoaiDongHo.Dien, official: false, 999m);  // phòng 2: ảnh không chính thức, không được dùng
            Image(103, 2, LoaiDongHo.Dien, official: true, 260m);
            Image(104, 2, LoaiDongHo.Nuoc, official: true, 95m);

            var request = ManualApproveRequestForRooms(10, 2026, 1, 2);
            var item2 = request.DanhSachPhong.Single(i => i.PhongTroId == 2);
            item2.DienMode = MeterReadingSubmissionMode.OfficialImage;
            item2.NuocMode = MeterReadingSubmissionMode.OfficialImage;

            var result = await _service.ApprovePeriodsAsync(request, 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((160m, 65m), (room1.ChiSoDienMoi, room1.ChiSoNuocMoi));
            Assert.Equal((260m, 95m), (room2.ChiSoDienMoi, room2.ChiSoNuocMoi));
            Assert.False(_store.Images[101].DuocChonLamChiSoChinhThuc);
            Assert.True(_store.Images[103].DuocChonLamChiSoChinhThuc);
            Assert.True(_store.Images[104].DuocChonLamChiSoChinhThuc);
            var items = result.Data!.Items.ToDictionary(i => i.PhongTroId);
            Assert.Equal(MeterReadingEvidenceSource.NhapThuCong, items[1].NguonBangChungDien);
            Assert.Equal(MeterReadingEvidenceSource.AnhXacNhan, items[2].NguonBangChungDien);
            Assert.Equal(MeterReadingEvidenceSource.AnhXacNhan, items[2].NguonBangChungNuoc);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_ManyRooms_DoesNotReadChecksRoomByRoom()
        {
            for (var id = 1; id <= 3; id++)
            {
                _store.RoomBranches[id] = 10;
                _store.ContractMonths.Add((id, 9, 2026));
                _store.Periods[10 + id] = new DichVuDienNuocCuaPhong
                {
                    DichVuDienNuocCuaPhongId = 10 + id, PhongTroId = id, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                };
                _store.Periods[id] = new DichVuDienNuocCuaPhong
                {
                    DichVuDienNuocCuaPhongId = id, PhongTroId = id, Thang = 10, Nam = 2026,
                    ChiSoDienCu = 100, ChiSoNuocCu = 50, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                };
            }
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 3, 1, 2), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(new[] { 1, 2, 3 }, result.Data!.Items.Select(i => i.PhongTroId));
            Assert.All(new[] { 1, 2, 3 }, id => Assert.Equal(TrangThaiGhiNhan.DaDuyet, _store.Periods[id].TrangThaiGhiNhan));
            Assert.Equal(0, _store.GetPeriodForUpdateCalls);
            Assert.Equal(0, _store.GetImagesForUpdateCalls);
            Assert.Equal(0, _store.HasSubsequentPeriodCalls);
            Assert.Equal(0, _store.HasLockedInvoiceCalls);
            Assert.Equal(0, _store.HasContractInMonthCalls);
            Assert.Equal(0, _store.GetPeriodRecordCalls);
            Assert.Equal(1, _store.GetPeriodsForUpdateBatchCalls);
            Assert.Equal(1, _store.GetImagesForUpdateBatchCalls);
            Assert.Equal(1, _store.SubsequentBatchCalls);
            Assert.Equal(1, _store.LockedInvoiceBatchCalls);
            Assert.Equal(1, _store.ContractInMonthBatchCalls);
            Assert.Equal(1, _store.GetPeriodRecordsBatchCalls);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_ManyRoomsWithoutPeriod_CreatesDrafts_ReadsBranchPricesOnce()
        {
            for (var id = 1; id <= 3; id++)
            {
                _store.RoomBranches[id] = 10;
            }
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequestForRooms(10, 2026, 1, 2, 3), 2);

            Assert.True(result.Success, result.Message);
            var created = _store.Periods.Values.Where(p => p.Thang == 10 && p.Nam == 2026).OrderBy(p => p.PhongTroId).ToList();
            Assert.Equal(new[] { 1, 2, 3 }, created.Select(p => p.PhongTroId));
            Assert.All(created, p =>
            {
                Assert.Equal(TrangThaiGhiNhan.DaDuyet, p.TrangThaiGhiNhan);
                Assert.Equal(3500m, p.DonGiaDien);
                Assert.Equal(20000m, p.DonGiaNuoc);
                Assert.Equal((100m, 160m), (p.ChiSoDienCu, p.ChiSoDienMoi));
                Assert.Equal((50m, 65m), (p.ChiSoNuocCu, p.ChiSoNuocMoi));
            });
            Assert.Equal(2, _store.GetServicePriceCalls);
        }

        private static ApproveMeterPeriodsRequest ManualApproveRequest(int thang, int nam, int phongTroId = 1) => new()
        {
            Thang = thang,
            Nam = nam,
            DanhSachPhong = new List<ApproveMeterPeriodItem>
            {
                new()
                {
                    PhongTroId = phongTroId,
                    DienMode = MeterReadingSubmissionMode.Manual,
                    ChiSoDienMoiThuCong = 160m,
                    LyDoDienThuCong = "Ghi trực tiếp",
                    NuocMode = MeterReadingSubmissionMode.Manual,
                    ChiSoNuocMoiThuCong = 65m,
                    LyDoNuocThuCong = "Ghi trực tiếp"
                }
            }
        };

        [Fact]
        public async Task ApprovePeriodsAsync_WhenPeriodIsInTheFuture_ReturnsFail_AndDoesNotApprove()
        {
            // Giờ cố định của test: 15/10/2026 (giờ VN), nên tháng 11/2026 là kỳ chưa tới.
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 11, Nam = 2026, ChiSoDienCu = 100, ChiSoNuocCu = 50 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequest(11, 2026), 2);

            Assert.False(result.Success);
            Assert.Contains("chưa tới", result.Message);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenPreviousMonthHadContractButIsNotApproved_ReturnsFail_AndRollsBack()
        {
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap };
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 2, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100, ChiSoNuocCu = 50 };
            _store.Periods[2] = period;
            _store.ContractMonths.Add((1, 9, 2026));
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequest(10, 2026), 2);

            Assert.False(result.Success);
            Assert.Contains("09/2026", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        private (DichVuDienNuocCuaPhong Room1, DichVuDienNuocCuaPhong Room2) SetupTwoRoomsInOctober()
        {
            var room1 = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100, ChiSoNuocCu = 50 };
            var room2 = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 2, PhongTroId = 2, Thang = 10, Nam = 2026, ChiSoDienCu = 200, ChiSoNuocCu = 80 };
            _store.Periods[1] = room1;
            _store.Periods[2] = room2;
            _store.RoomBranches[1] = 10;
            _store.RoomBranches[2] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));
            return (room1, room2);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenOnlyOneRoomIsSent_LeavesOtherRoomsOfTheBranchUntouched()
        {
            var (room1, room2) = SetupTwoRoomsInOctober();

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequest(10, 2026, phongTroId: 1), 2);

            Assert.True(result.Success);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, room1.TrangThaiGhiNhan);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, room2.TrangThaiGhiNhan);
            Assert.Equal(0m, room2.ChiSoDienMoi);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenOneRoomOfTheGroupFails_RollsBackTheWholeGroup()
        {
            var (_, room2) = SetupTwoRoomsInOctober();
            // Phòng 2 có hợp đồng tháng trước nhưng kỳ 09/2026 chưa chốt, nên không được duyệt.
            _store.Periods[3] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 3, PhongTroId = 2, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap };
            _store.ContractMonths.Add((2, 9, 2026));

            var request = ManualApproveRequest(10, 2026, phongTroId: 1);
            request.DanhSachPhong = request.DanhSachPhong.Concat(ManualApproveRequest(10, 2026, phongTroId: 2).DanhSachPhong).ToList();

            var result = await _service.ApprovePeriodsAsync(request, 2);

            Assert.False(result.Success);
            Assert.Contains("09/2026", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
            // Không có lần lưu nào: đối tượng trong bộ nhớ của fake có thể đã bị sửa, nhưng giao dịch bị hủy nên DB không đổi.
            Assert.Equal(0, _uow.SaveChangesCallCount);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, room2.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_ErrorMessage_UsesRoomNumberInsteadOfInternalId()
        {
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 7, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap };
            _store.Periods[2] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 2, PhongTroId = 7, Thang = 10, Nam = 2026, ChiSoDienCu = 100, ChiSoNuocCu = 50 };
            _store.ContractMonths.Add((7, 9, 2026));
            _store.RoomBranches[7] = 10;
            _store.RoomNumbers[7] = "A101";
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequest(10, 2026, phongTroId: 7), 2);

            Assert.False(result.Success);
            Assert.Contains("Phòng A101", result.Message);
            Assert.DoesNotContain("Phòng 7", result.Message);
        }

        [Fact]
        public async Task ConfirmImageAsync_WhenNoteExceedsColumnLimit_ReturnsFailWithoutTouchingStore()
        {
            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150m, GhiChu = new string('x', 1001) }, 2);

            Assert.False(result.Success);
            Assert.Contains("1000", result.Message);
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenPreviousMonthHadContractAndNoRecord_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 2, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100, ChiSoNuocCu = 50 };
            _store.Periods[2] = period;
            _store.ContractMonths.Add((1, 9, 2026));
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequest(10, 2026), 2);

            Assert.False(result.Success);
            Assert.NotEqual(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenPreviousMonthApproved_Succeeds()
        {
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 9, Nam = 2026, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet };
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 2, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100, ChiSoNuocCu = 50 };
            _store.Periods[2] = period;
            _store.ContractMonths.Add((1, 9, 2026));
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var result = await _service.ApprovePeriodsAsync(ManualApproveRequest(10, 2026), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_ManualModeWithOldOfficialImage_DoesNotCallUpdateImageOrUpdatePeriodRecord()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var oldOfficialDien = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 10,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 150m
            };
            var oldOfficialNuoc = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 11,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Nuoc,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 60m
            };
            _store.Images[10] = oldOfficialDien;
            _store.Images[11] = oldOfficialNuoc;

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 160m,
                        LyDoDienThuCong = "Đổi sang nhập tay",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 65m,
                        LyDoNuocThuCong = "Đổi sang nhập tay"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.False(oldOfficialDien.DuocChonLamChiSoChinhThuc);
            Assert.False(oldOfficialNuoc.DuocChonLamChiSoChinhThuc);
            Assert.Equal(0, _store.UpdateImageCallCount);
            Assert.Equal(0, _store.UpdatePeriodRecordCallCount);
        }

        [Fact]
        public async Task UploadImageAsync_WhenPhase3DbFailsAndImageCannotBeReloaded_ReturnsFail()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            // Làm fail ở lần SaveChanges thứ hai (Pha 3)
            _uow.ThrowOnSaveCallIndex = 1;
            // Làm cho GetImageByIdAsync ở catch block trả về null
            _store.ReturnNullOnSecondGetImageById = true;

            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Null(result.Data);
            Assert.Contains("Lỗi khi lưu kết quả nhận diện", result.Message);
        }

        [Theory]
        [InlineData((MeterReadingSubmissionMode)0, MeterReadingSubmissionMode.Manual)]
        [InlineData(MeterReadingSubmissionMode.Manual, (MeterReadingSubmissionMode)3)]
        public async Task ApprovePeriodsAsync_WhenInvalidMode_ReturnsFail(MeterReadingSubmissionMode dienMode, MeterReadingSubmissionMode nuocMode)
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = dienMode,
                        ChiSoDienMoiThuCong = 150m,
                        LyDoDienThuCong = "Lý do điện",
                        NuocMode = nuocMode,
                        ChiSoNuocMoiThuCong = 60m,
                        LyDoNuocThuCong = "Lý do nước"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("không hợp lệ", result.Message.ToLowerInvariant());
            Assert.Contains("1", result.Message);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenDuplicateRoomInBatch_ReturnsFail()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 150m,
                        LyDoDienThuCong = "Lý do 1",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 60m,
                        LyDoNuocThuCong = "Lý do 1"
                    },
                    new()
                    {
                        PhongTroId = 1, // Trùng phòng 1
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 160m,
                        LyDoDienThuCong = "Lý do 2",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 70m,
                        LyDoNuocThuCong = "Lý do 2"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("Phòng 1 xuất hiện nhiều lần trong yêu cầu", result.Message);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenApprovedTwiceWithSameReason_DoesNotDuplicateNotes()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 160m,
                        LyDoDienThuCong = "Đồng hồ mờ",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 65m,
                        LyDoNuocThuCong = "Khách vắng nhà"
                    }
                }
            };

            // Lần duyệt 1
            var res1 = await _service.ApprovePeriodsAsync(req, 2);
            Assert.True(res1.Success);
            Assert.Equal("[Điện thủ công: Đồng hồ mờ]; [Nước thủ công: Khách vắng nhà]", period.GhiChuDuyet);

            // Lần duyệt 2 (duyệt lại cùng lý do)
            var res2 = await _service.ApprovePeriodsAsync(req, 2);
            Assert.True(res2.Success);
            // Ghi chú không được lặp lại
            Assert.Equal("[Điện thủ công: Đồng hồ mờ]; [Nước thủ công: Khách vắng nhà]", period.GhiChuDuyet);
        }

        [Fact]
        public async Task UploadImageAsync_WhenInvalidLoaiDongHo_ReturnsFail()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = (LoaiDongHo)999, // Invalid enum
                File = file
            };

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("Loại đồng hồ không hợp lệ", result.Message);
        }

        [Fact]
        public async Task UploadImageAsync_WhenFileSizeExceedsLimit_ReturnsFail()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 10 * 1024 * 1024); // 10MB > 5MB
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("vượt quá giới hạn", result.Message);
        }

        [Theory]
        [InlineData("application/pdf")]
        [InlineData("text/plain")]
        [InlineData("image/gif")]
        public async Task UploadImageAsync_WhenDisallowedContentType_ReturnsFail(string badContentType)
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.file", badContentType, 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("Định dạng tệp không được hỗ trợ", result.Message);
        }

        [Fact]
        public async Task UploadImageAsync_WhenCallerCancels_RethrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.UploadImageAsync(req, 2, cts.Token));
        }

        [Fact]
        public async Task UploadImageAsync_WhenDbFails_ReturnsSafeErrorMessage_AndDoesNotLeakRawException()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _uow.ThrowOnNextSave = true; // Mô phỏng lỗi DB save lần 1

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.DoesNotContain("Simulated Database Save Failure", result.Message);
            Assert.Contains("Lỗi khi lưu thông tin ảnh vào cơ sở dữ liệu", result.Message);
            Assert.Contains("meters/img1", _storage.DeletedPublicIds); // Cleanup best-effort
        }

        [Fact]
        public async Task UploadImageAsync_WhenOcrFails_RecordsSafeLoiMessage_AndReturnsDescriptiveMessage()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _ocr.ThrowOnProcess = true; // Mô phỏng exception khi chạy OCR

            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success); // Ảnh đã được lưu
            Assert.Equal(TrangThaiXuLyAnhChiSo.Loi, result.Data!.TrangThaiXuLy);
            Assert.Contains("gặp sự cố", result.Message);
            var savedImg = _store.Images.Values.Last();
            Assert.DoesNotContain("AI service crashed", savedImg.ThongBaoLoi); // Không lưu raw exception message
            Assert.Contains("lỗi hệ thống", savedImg.ThongBaoLoi);
        }

        [Fact]
        public async Task RetryOcrAsync_WhenCallerCancels_RethrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 10,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi
            };
            _store.Images[10] = img;
            _store.Periods[1] = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1 };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.RetryOcrAsync(10, 2, cts.Token));
        }

        [Fact]
        public async Task RetryOcrAsync_WhenImageAlreadyConfirmed_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 150m
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.False(result.Success);
            Assert.Contains("xác nhận", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task RetryOcrAsync_WhenPeriodHasSubsequentPeriod_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _store.SubsequentPeriods.Add((1, 10, 2026));

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.False(result.Success);
            Assert.Contains("kỳ sau", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task RetryOcrAsync_WhenPeriodHasLockedInvoice_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _store.NonDraftInvoicePeriodIds.Add(1);

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.Loi
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.False(result.Success);
            Assert.Contains("hóa đơn", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task RetryOcrAsync_WhenPhase3DetectsDifferentStamp_ReturnsFailAndDoesNotOverwrite()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                PublicId = "p1",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));
            _ocr.ResultToReturn = MeterOcrResult.Readable(180m, 0.95);

            // Mô phỏng: trong lúc OCR đang chạy ở Pha 2, một sự kiện khác can thiệp đổi dấu NgayXuLy hoặc đổi trạng thái
            _ocr.OnProcess = () =>
            {
                img.NgayXuLy = DateTime.UtcNow.AddMinutes(5); // Thay đổi stamp
            };

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.False(result.Success);
            Assert.Contains("thao tác khác", result.Message.ToLowerInvariant());
            Assert.Null(img.GiaTriAIGoiY); // Kết quả không được ghi
        }

        [Fact]
        public async Task RetryOcrAsync_WhenPhase3DbSaveFails_ReturnsSafeOkMessage()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                PublicId = "p1",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));
            _ocr.ResultToReturn = MeterOcrResult.Readable(180m, 0.95);

            // Trong lúc lưu lần 2 (Pha 3), DB save fail
            _ocr.OnProcess = () =>
            {
                _uow.ThrowOnNextSave = true;
            };

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.True(result.Success);
            Assert.Contains("kết quả nhận diện chưa được ghi", result.Message);
        }

        [Fact]
        public async Task UploadImageAsync_WhenInvoiceBecomesNonDraftDuringUpload_RejectsAndDeletesStorageFile()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2 }), "test.jpg", "image/jpeg", 2);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026
            };
            _store.Periods[1] = period;

            // Mô phỏng: ngoài transaction kiểm tra ban đầu là chưa có invoice bị khóa,
            // nhưng khi vừa upload storage xong, invoice chuyển sang non-draft (khóa kỳ)
            // Ta hook vào lúc upload storage để kích hoạt invoice lock
            _storage.ReadBytesToReturn = new byte[] { 1, 2 };
            _storage.ThrowOnUpload = false;

            // Thêm invoice lock ngay khi storage upload chạy
            _storage.OnUpload = () =>
            {
                _store.NonDraftInvoicePeriodIds.Add(1);
            };

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("hóa đơn", result.Message.ToLowerInvariant());
            Assert.Contains("meters/img1", _storage.DeletedPublicIds); // File vừa upload được cleanup
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenCreatingDraftPeriod_SetsNguoiTaoIdAndNgayTao()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((5, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 120m,
                        LyDoDienThuCong = "Nhập tay điện",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 60m,
                        LyDoNuocThuCong = "Nhập tay nước"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 5); // actorUserId = 5

            Assert.True(result.Success);
            var createdPeriod = _store.Periods.Values.FirstOrDefault(p => p.PhongTroId == 1 && p.Thang == 10 && p.Nam == 2026);
            Assert.NotNull(createdPeriod);
            Assert.Equal(5, createdPeriod.NguoiTaoId);
            Assert.True(createdPeriod.NgayTao > DateTime.MinValue);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void MeterReadingWorkflowService_Constructor_ThrowsArgumentNullException_WhenAnyDependencyIsNull(int nullIndex)
        {
            var fakeHoaDonStore = new Fakes.FakeHoaDonStore();
            var calculator = new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MeterReadingWorkflowService>.Instance;
            var options = new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.MeterImageOptions();

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new MeterReadingWorkflowService(
                    nullIndex == 0 ? null! : _store,
                    nullIndex == 1 ? null! : _storage,
                    nullIndex == 2 ? null! : _ocr,
                    nullIndex == 3 ? null! : _access,
                    nullIndex == 4 ? null! : _uow,
                    nullIndex == 5 ? null! : fakeHoaDonStore,
                    nullIndex == 6 ? null! : calculator,
                    nullIndex == 7 ? null! : logger,
                    nullIndex == 8 ? null! : options,
                    nullIndex == 9 ? null! : new Fakes.FakeMeterConsumptionHistoryStore(),
                    nullIndex == 10 ? null! : new QuanLyChoThuePhongTroWeb.Application.Common.Configurations.AutoInvoiceOptions()
                );
            });
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void DienNuocService_Constructor_ThrowsArgumentNullException_WhenAnyDependencyIsNull(int nullIndex)
        {
            var fakeDienNuocStore = new Fakes.FakeDienNuocStore();
            var fakeAccess = new FakeEmployeeAccessService();
            var fakeWorkflow = new FakeMeterReadingWorkflowService();

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new DienNuocService(
                    nullIndex == 0 ? null! : fakeDienNuocStore,
                    nullIndex == 1 ? null! : fakeAccess,
                    nullIndex == 2 ? null! : fakeWorkflow
                );
            });
        }

        [Fact]
        public async Task ConfirmImageAsync_WhenValueHasMoreThan3DecimalPlaces_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026, ChiSoDienCu = 100 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 150.1234m };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("3 chữ số thập phân", result.Message);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WithBothOfficialImages_UsesServerConfirmedValues()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var imgDien = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 101,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 180.5m
            };
            var imgNuoc = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 102,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Nuoc,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 75.2m
            };
            _store.Images[101] = imgDien;
            _store.Images[102] = imgNuoc;

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.OfficialImage,
                        NuocMode = MeterReadingSubmissionMode.OfficialImage
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(180.5m, period.ChiSoDienMoi);
            Assert.Equal(75.2m, period.ChiSoNuocMoi);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WithOneOfficialImageAndOneManualInputWithReason_Succeeds()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var imgDien = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 101,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 180m
            };
            _store.Images[101] = imgDien;

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.OfficialImage,
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 60m,
                        LyDoNuocThuCong = "Đồng hồ nước hỏng van kim"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(180m, period.ChiSoDienMoi);
            Assert.Equal(60m, period.ChiSoNuocMoi);
            Assert.Contains("Đồng hồ nước hỏng van kim", period.GhiChuDuyet);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenModeIsOfficialImage_IgnoresClientProvidedManualValue()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var imgDien = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 101,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 175m
            };
            var imgNuoc = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 102,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Nuoc,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 80m
            };
            _store.Images[101] = imgDien;
            _store.Images[102] = imgNuoc;

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.OfficialImage,
                        ChiSoDienMoiThuCong = 999m,
                        LyDoDienThuCong = "Lý do thừa",
                        NuocMode = MeterReadingSubmissionMode.OfficialImage,
                        ChiSoNuocMoiThuCong = 888m
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(175m, period.ChiSoDienMoi);
            Assert.Equal(80m, period.ChiSoNuocMoi);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenManualZeroReadingWithReasonAndNotLessThanOld_Succeeds()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 0m,
                ChiSoNuocCu = 0m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 0m,
                        LyDoDienThuCong = "Phòng mới nhận bàn giao chưa dùng điện",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 0m,
                        LyDoNuocThuCong = "Phòng mới nhận bàn giao chưa dùng nước"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(0m, period.ChiSoDienMoi);
            Assert.Equal(0m, period.ChiSoNuocMoi);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenAlreadyApprovedAndNotLocked_SucceedsViaDuyetLai()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                ChiSoDienMoi = 150,
                ChiSoNuocMoi = 60,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 160m,
                        LyDoDienThuCong = "Cập nhật lại do đọc nhầm",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 65m,
                        LyDoNuocThuCong = "Cập nhật lại nước"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(160m, period.ChiSoDienMoi);
            Assert.Equal(65m, period.ChiSoNuocMoi);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_RecalculatesLinkedDraftInvoice()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                DonGiaDien = 3500m,
                DonGiaNuoc = 20000m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var draftInvoice = new HoaDon
            {
                HoaDonId = 10,
                HopDongId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                DichVuDienNuocCuaPhongId = 1,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new()
                    {
                        ChiTietHoaDonId = 1,
                        TenDichVu = "Tiền điện",
                        DonGia = 3500m,
                        SoLuong = 0m,
                        TongTien = 0m
                    },
                    new()
                    {
                        ChiTietHoaDonId = 2,
                        TenDichVu = "Tiền nước",
                        DonGia = 20000m,
                        SoLuong = 0m,
                        TongTien = 0m
                    }
                },
                TongTien = 0m
            };
            _hoaDonStore.Invoices.Add(draftInvoice);

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.Manual,
                        ChiSoDienMoiThuCong = 150m,
                        LyDoDienThuCong = "Lý do điện",
                        NuocMode = MeterReadingSubmissionMode.Manual,
                        ChiSoNuocMoiThuCong = 60m,
                        LyDoNuocThuCong = "Lý do nước"
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.True(draftInvoice.TongTien > 0m);
            var dienItem = draftInvoice.ChiTietHoaDonDichVus.First(c => c.TenDichVu.Contains("điện", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(50m, dienItem.SoLuong);
            Assert.Equal(50m * 3500m, dienItem.TongTien);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenOfficialImageConfirmedValueIsSmallerThanOldReading_ReturnsDescriptiveFail()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100,
                ChiSoNuocCu = 50,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var imgDien = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 101,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 90m
            };
            var imgNuoc = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 102,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Nuoc,
                DuocChonLamChiSoChinhThuc = true,
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 60m
            };
            _store.Images[101] = imgDien;
            _store.Images[102] = imgNuoc;

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    new()
                    {
                        PhongTroId = 1,
                        DienMode = MeterReadingSubmissionMode.OfficialImage,
                        NuocMode = MeterReadingSubmissionMode.OfficialImage
                    }
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("nhỏ hơn chỉ số điện cũ", result.Message);
        }

        [Theory]
        [InlineData("image/jpeg", "meter.jpg", 100, true)]
        [InlineData("image/png", "meter.png", 100, true)]
        [InlineData("image/webp", "meter.webp", 100, true)]
        [InlineData("image/jpeg", "empty.jpg", 0, false)]
        public async Task UploadImageAsync_ValidatesFileFormats_AcceptsJpegPngWebp_RejectsEmpty(string contentType, string fileName, int length, bool shouldSucceed)
        {
            var file = new UploadFile(new MemoryStream(new byte[length]), fileName, contentType, length);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            var result = await _service.UploadImageAsync(req, 2);

            if (shouldSucceed)
            {
                Assert.True(result.Success);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Contains("không hợp lệ", result.Message.ToLowerInvariant());
            }
        }

        private UploadMeterImageRequest StaffUploadRequest(byte[] bytes)
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            return new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = new UploadFile(new MemoryStream(bytes), "meter.jpg", "image/jpeg", bytes.Length)
            };
        }

        [Fact]
        public async Task UploadImageAsync_WithJpegBytes_RunsOcrOnUploadedBytes_WithoutReadingBackFromStorage()
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8 };
            var req = StaffUploadRequest(bytes);

            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(bytes, _storage.UploadedBytes); // kho lưu trữ vẫn nhận đủ tệp
            Assert.Equal(0, _storage.ReadCallCount);
            Assert.Equal(bytes, _ocr.LastImageBytes);
            Assert.Equal("image/jpeg", _ocr.LastContentType);
        }

        [Fact]
        public async Task UploadImageAsync_WhenBytesAreNotJpegPngWebp_StillReadsBackFromStorage()
        {
            // Đuôi và content-type khai báo là JPG nhưng nội dung không phải: giữ đường cũ, đọc lại từ kho lưu trữ.
            var req = StaffUploadRequest(new byte[] { 1, 2, 3 });

            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(1, _storage.ReadCallCount);
            Assert.Equal(_storage.ReadBytesToReturn, _ocr.LastImageBytes);
            Assert.Equal(_storage.ReadContentTypeToReturn, _ocr.LastContentType);
        }

        [Fact]
        public async Task UploadImageAsync_WhenStorageFails_DoesNotPersistAnyRecordToDb()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2, 3 }), "meter.jpg", "image/jpeg", 3);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _storage.ThrowOnUpload = true;

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Empty(_store.Images);
        }

        [Fact]
        public async Task UploadImageAsync_WhenCleanupFails_ReturnsOriginalDbErrorAndLogsWarning()
        {
            var file = new UploadFile(new MemoryStream(new byte[] { 1, 2, 3 }), "meter.jpg", "image/jpeg", 3);
            var req = new UploadMeterImageRequest
            {
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                LoaiDongHo = LoaiDongHo.Dien,
                File = file
            };
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            _uow.ThrowOnNextSave = true;
            _storage.ThrowOnDelete = true;

            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("Lỗi khi lưu thông tin ảnh vào cơ sở dữ liệu", result.Message);
        }

        [Fact]
        public async Task UploadImageAsync_WhenNewImageUploadedForSamePeriod_CreatesNewRecordWithoutModifyingExisting()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            var req1 = CreateValidRequest(1);
            var res1 = await _service.UploadImageAsync(req1, 2);
            Assert.True(res1.Success);
            var img1Id = res1.Data!.AnhChiSoDongHoId;
            var img1 = _store.Images[img1Id];

            var req2 = CreateValidRequest(1);
            var res2 = await _service.UploadImageAsync(req2, 2);
            Assert.True(res2.Success);
            var img2Id = res2.Data!.AnhChiSoDongHoId;

            Assert.NotEqual(img1Id, img2Id);
            Assert.Equal(2, _store.Images.Count);
            Assert.Equal("https://storage.test/img1.jpg", img1.Url);
        }

        [Fact]
        public async Task RetryOcrAsync_WhenStampDiffersByOneMicrosecond_RejectsAndReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                Url = "http://test",
                PublicId = "p1",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterRetryOcr));
            _ocr.ResultToReturn = MeterOcrResult.Readable(180m, 0.95);

            // Hook vào lúc OCR đang chạy ở Pha 2 để làm lệch NgayXuLy trong store đi 1 micro giây (10 ticks)
            _ocr.OnProcess = () =>
            {
                img.NgayXuLy = img.NgayXuLy!.Value.AddTicks(10);
            };

            var result = await _service.RetryOcrAsync(100, 2);

            Assert.False(result.Success);
            Assert.Contains("thao tác khác", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenValid_StaffOrAdmin_UpdatesImageAndPeriod()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 70m
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                DuocChonLamChiSoChinhThuc = true,
                GiaTriXacNhan = 150m,
                NguoiXacNhanId = 1
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new CorrectConfirmedMeterImageRequest(100, 180m, "Số trên công tơ là 180");
            var result = await _service.CorrectConfirmedImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(180m, img.GiaTriXacNhan);
            Assert.Equal(2, img.NguoiXacNhanId);
            Assert.Contains("[Sửa 150 → 180]", img.GhiChuXacNhan);
            Assert.Equal(180m, period.ChiSoDienMoi);
            Assert.Equal(70m, period.ChiSoNuocMoi); // Nước giữ nguyên
            Assert.Equal(0, _store.UpdateImageCallCount);
            Assert.Equal(0, _store.UpdatePeriodRecordCallCount);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenActorHasNoPermission_ReturnsFail()
        {
            var period = new DichVuDienNuocCuaPhong { DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026 };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                DuocChonLamChiSoChinhThuc = true,
                GiaTriXacNhan = 150m
            };
            _store.Images[100] = img;
            // Actor 99 không có quyền

            var req = new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa lại");
            var result = await _service.CorrectConfirmedImageAsync(req, 99);

            Assert.False(result.Success);
            Assert.Contains("không có quyền", result.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenValidationFails_ReturnsFailWithoutException()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                DuocChonLamChiSoChinhThuc = true,
                GiaTriXacNhan = 150m
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            // 1. Lý do rỗng
            var r1 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "  "), 2);
            Assert.False(r1.Success);
            Assert.Contains("lý do", r1.Message.ToLowerInvariant());

            // 2. Lý do quá 500 ký tự
            var longReason = new string('A', 501);
            var r2 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, longReason), 2);
            Assert.False(r2.Success);
            Assert.Contains("500", r2.Message);

            // 3. Số mới < số cũ
            var r3 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 90m, "Số nhỏ hơn cũ"), 2);
            Assert.False(r3.Success);
            Assert.Contains("nhỏ hơn", r3.Message.ToLowerInvariant());

            // 4. Số mới trùng số hiện tại
            var r4 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 150m, "Số trùng"), 2);
            Assert.False(r4.Success);
            Assert.Contains("trùng", r4.Message.ToLowerInvariant());

            // 5. Số mới quá 3 chữ số thập phân
            var r5 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180.1234m, "Quá 3 số lẻ"), 2);
            Assert.False(r5.Success);
            Assert.Contains("thập phân", r5.Message.ToLowerInvariant());

            // 6. Có kỳ sau
            _store.SubsequentPeriods.Add((1, 10, 2026));
            var r6 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Hợp lệ nhưng có kỳ sau"), 2);
            Assert.False(r6.Success);
            Assert.Contains("kỳ sau", r6.Message.ToLowerInvariant());
            _store.SubsequentPeriods.Clear();

            // 7. Có hóa đơn khóa
            _store.NonDraftInvoicePeriodIds.Add(1);
            var r7 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Hợp lệ nhưng hóa đơn khóa"), 2);
            Assert.False(r7.Success);
            Assert.Contains("hóa đơn", r7.Message.ToLowerInvariant());
            _store.NonDraftInvoicePeriodIds.Clear();

            // 8. Ảnh không chính thức
            img.DuocChonLamChiSoChinhThuc = false;
            var r8 = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Ảnh phụ"), 2);
            Assert.False(r8.Success);
            Assert.Contains("chính thức", r8.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenPeriodDaDuyet_CallsDuyetLai_KeepsStateDaDuyetAndOldNotes()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                NguoiDuyetId = 1,
                GhiChuDuyet = "Ghi chú ban đầu"
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                DuocChonLamChiSoChinhThuc = true,
                GiaTriXacNhan = 150m
            };
            _store.Images[100] = img;
            _access.Permissions.Add((5, 10, EmployeeActionCodes.MeterReview));

            var req = new CorrectConfirmedMeterImageRequest(100, 175m, "Điều chỉnh chỉ số");
            var result = await _service.CorrectConfirmedImageAsync(req, 5); // actorUserId = 5

            Assert.True(result.Success);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(5, period.NguoiDuyetId);
            Assert.Equal("Ghi chú ban đầu", period.GhiChuDuyet);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_RecalculatesDraftInvoice()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                DonGiaDien = 3000m
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                DuocChonLamChiSoChinhThuc = true,
                GiaTriXacNhan = 150m
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var invoice = new HoaDon
            {
                HoaDonId = 1,
                DichVuDienNuocCuaPhongId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new() { TenDichVu = "Tiền điện", SoLuong = 50, DonGia = 3000m, TongTien = 150000m }
                }
            };
            _hoaDonStore.Invoices.Add(invoice);

            var req = new CorrectConfirmedMeterImageRequest(100, 160m, "Tăng 10 số điện");
            var result = await _service.CorrectConfirmedImageAsync(req, 2);

            Assert.True(result.Success);
            var electricDetail = invoice.ChiTietHoaDonDichVus.First(d => d.TenDichVu.StartsWith("Tiền điện"));
            Assert.Equal(60m, electricDetail.SoLuong);
            Assert.Equal(180000m, electricDetail.TongTien);
        }

        [Theory]
        [InlineData("Điện tháng 10 (100 → 150)")]
        [InlineData("Điện: 50 x 3000")]
        [InlineData("tiền điện phòng")]
        public async Task CorrectConfirmedImageAsync_WhenDraftElectricLineWasRenamed_UpdatesThatLineInsteadOfAddingSecond(string renamedLine)
        {
            var (invoice, _) = SetupDraftWithLine(renamedLine);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 160m, "Tăng 10 số điện"), 2);

            Assert.True(result.Success);
            var line = Assert.Single(invoice.ChiTietHoaDonDichVus);
            Assert.Equal(60m, line.SoLuong);
            Assert.Equal(180000m, line.TongTien);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenDraftHasUnrelatedDienThoaiLine_KeepsItAndAddsElectricLine()
        {
            var (invoice, _) = SetupDraftWithLine("Điện thoại bàn");

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 160m, "Tăng 10 số điện"), 2);

            Assert.True(result.Success);
            var phone = invoice.ChiTietHoaDonDichVus.Single(d => d.TenDichVu == "Điện thoại bàn");
            Assert.Equal(150000m, phone.TongTien);
            Assert.Equal(2, invoice.ChiTietHoaDonDichVus.Count);
        }

        private (HoaDon Invoice, DichVuDienNuocCuaPhong Period) SetupDraftWithLine(string lineName)
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 150m, DonGiaDien = 3000m
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            _store.Images[100] = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100, DichVuDienNuocCuaPhongId = 1, LoaiDongHo = LoaiDongHo.Dien, Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan, DuocChonLamChiSoChinhThuc = true, GiaTriXacNhan = 150m
            };
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var invoice = new HoaDon
            {
                HoaDonId = 1,
                DichVuDienNuocCuaPhongId = 1,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new() { TenDichVu = lineName, SoLuong = 50, DonGia = 3000m, TongTien = 150000m }
                }
            };
            _hoaDonStore.Invoices.Add(invoice);
            return (invoice, period);
        }

        #region Cập nhật dây chuyền kỳ sau (kỳ sau còn là bản nháp)

        private DichVuDienNuocCuaPhong SetupCorrectionWithLaterPeriods(
            out AnhChiSoDongHo image,
            params DichVuDienNuocCuaPhong[] later)
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 150m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            foreach (var l in later) _store.Periods[l.DichVuDienNuocCuaPhongId] = l;

            image = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100, DichVuDienNuocCuaPhongId = 1, LoaiDongHo = LoaiDongHo.Dien, Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan, DuocChonLamChiSoChinhThuc = true, GiaTriXacNhan = 150m, NguoiXacNhanId = 1
            };
            _store.Images[100] = image;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));
            return period;
        }

        private static DichVuDienNuocCuaPhong LaterPeriod(int id, int thang, decimal dienCu, decimal dienMoi, decimal nuocCu, decimal nuocMoi) => new()
        {
            DichVuDienNuocCuaPhongId = id, PhongTroId = 1, Thang = thang, Nam = 2026,
            ChiSoDienCu = dienCu, ChiSoDienMoi = dienMoi, ChiSoNuocCu = nuocCu, ChiSoNuocMoi = nuocMoi
        };

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenNextPeriodIsUntouchedDraft_ShiftsItsOldAndNewReading()
        {
            var next = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            var period = SetupCorrectionWithLaterPeriods(out _, next);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success);
            Assert.Equal(180m, period.ChiSoDienMoi);
            Assert.Equal(180m, next.ChiSoDienCu);
            Assert.Equal(180m, next.ChiSoDienMoi);
            Assert.Equal(70m, next.ChiSoNuocCu);
            Assert.Equal(70m, next.ChiSoNuocMoi);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenNextPeriodHasOwnReading_OnlyShiftsOldReading()
        {
            var next = LaterPeriod(2, 11, 150m, 200m, 70m, 90m);
            SetupCorrectionWithLaterPeriods(out _, next);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success);
            Assert.Equal(180m, next.ChiSoDienCu);
            Assert.Equal(200m, next.ChiSoDienMoi);
            Assert.Equal(90m, next.ChiSoNuocMoi);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenNextPeriodReadingWouldBecomeNegative_FailsAndKeepsNextPeriod()
        {
            var next = LaterPeriod(2, 11, 150m, 170m, 70m, 90m);
            SetupCorrectionWithLaterPeriods(out _, next);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Equal(150m, next.ChiSoDienCu);
            Assert.Equal(170m, next.ChiSoDienMoi);
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_CascadesAcrossSeveralDraftPeriods()
        {
            var next1 = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 150m, 150m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next1, next2);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success);
            Assert.Equal((180m, 180m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((180m, 180m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenNoLaterPeriod_StillSucceeds()
        {
            var period = SetupCorrectionWithLaterPeriods(out _);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success);
            Assert.Equal(180m, period.ChiSoDienMoi);
        }

        // Mô phỏng đua: kiểm tra không khóa (HasSubsequentPeriodAsync) thấy kỳ sau còn nháp, nhưng khi đã giữ khóa
        // thì kỳ sau đã bị giao dịch khác duyệt/phát hành. Dây chuyền không được sửa số của kỳ đã chốt.
        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenNextPeriodApprovedAfterPreCheck_FailsAndKeepsNextPeriod()
        {
            var next = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            next.TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet;
            SetupCorrectionWithLaterPeriods(out _, next);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Equal((150m, 150m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenNextPeriodGetsIssuedInvoiceAfterPreCheck_FailsAndKeepsNextPeriod()
        {
            var next = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next);
            _store.NonDraftInvoicePeriodIds.Add(2);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.False(result.Success);
            Assert.Equal((150m, 150m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        private static ApproveMeterPeriodItem ManualApproveItem(int phongTroId, decimal dien, decimal nuoc) => new()
        {
            PhongTroId = phongTroId,
            DienMode = MeterReadingSubmissionMode.Manual,
            ChiSoDienMoiThuCong = dien,
            LyDoDienThuCong = "Đồng hồ điện bị mờ số",
            NuocMode = MeterReadingSubmissionMode.Manual,
            ChiSoNuocMoiThuCong = nuoc,
            LyDoNuocThuCong = "Khách không có nhà, ghi nhận trực tiếp"
        };

        [Fact]
        public async Task ApprovePeriodsAsync_WhenNextPeriodIsUntouchedDraft_ShiftsItsOldAndNewReading()
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 100m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 50m, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            var next = LaterPeriod(2, 11, 100m, 100m, 50m, 50m);
            _store.Periods[1] = period;
            _store.Periods[2] = next;
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem> { ManualApproveItem(1, 160m, 65m) }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal((160m, 160m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal((65m, 65m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
            Assert.True(_uow.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WhenNextPeriodHasSmallerOwnReading_RejectsWholeBatchAndRollsBack()
        {
            var period1 = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoNuocCu = 50m, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            var period3 = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 3, PhongTroId = 2, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 100m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 50m, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            var period4 = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 4, PhongTroId = 2, Thang = 11, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 120m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 50m
            };
            _store.Periods[1] = period1;
            _store.Periods[3] = period3;
            _store.Periods[4] = period4;
            _store.RoomBranches[1] = 10;
            _store.RoomBranches[2] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ApproveMeterPeriodsRequest
            {
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = new List<ApproveMeterPeriodItem>
                {
                    ManualApproveItem(1, 160m, 65m),
                    ManualApproveItem(2, 150m, 55m)
                }
            };

            var result = await _service.ApprovePeriodsAsync(req, 2);

            Assert.False(result.Success);
            Assert.Contains("Phòng 2", result.Message);
            Assert.Contains("11/2026", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.Equal((100m, 120m), (period4.ChiSoDienCu, period4.ChiSoDienMoi));
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        #endregion

        #region Đổi đồng hồ, quay vòng và cascade theo mốc

        private (DichVuDienNuocCuaPhong Period, AnhChiSoDongHo Image) SetupReset(
            decimal dienCu = 4520m,
            TrangThaiXuLyAnhChiSo status = TrangThaiXuLyAnhChiSo.DocDuoc,
            params DichVuDienNuocCuaPhong[] others)
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026,
                ChiSoDienCu = dienCu, ChiSoDienMoi = dienCu, ChiSoNuocCu = 50m, ChiSoNuocMoi = 50m
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            foreach (var o in others) _store.Periods[o.DichVuDienNuocCuaPhongId] = o;

            var image = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100, DichVuDienNuocCuaPhongId = 1, LoaiDongHo = LoaiDongHo.Dien, Url = "http://test",
                TrangThaiXuLy = status, GiaTriAIGoiY = 85.5m, DoTinCay = 0.95
            };
            _store.Images[100] = image;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));
            return (period, image);
        }

        private static ResetMeterBaselineRequest Reset(
            MeterBaselineResetType loai, decimal hienTai, decimal? soDau = null, decimal? soCuoi = null, string lyDo = "Thay đồng hồ mới")
            => new(100, loai, hienTai, soDau, soCuoi, lyDo);

        [Fact]
        public async Task ResetMeterBaseline_WithoutReviewPermission_Fails()
        {
            SetupReset();

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m), 99);

            Assert.False(result.Success);
            Assert.Contains("quyền", result.Message);
            Assert.False(_uow.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task ResetMeterBaseline_WhenLockedByLaterPeriod_Fails()
        {
            SetupReset();
            _store.SubsequentPeriods.Add((1, 10, 2026));

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m), 2);

            Assert.False(result.Success);
            Assert.Contains("Kỳ sau", result.Message);
        }

        [Fact]
        public async Task ResetMeterBaseline_WhenInvoiceLocked_Fails()
        {
            SetupReset();
            _store.NonDraftInvoicePeriodIds.Add(1);

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m), 2);

            Assert.False(result.Success);
        }

        [Theory]
        [InlineData(TrangThaiXuLyAnhChiSo.DaXacNhan)]
        [InlineData(TrangThaiXuLyAnhChiSo.DaThayThe)]
        [InlineData(TrangThaiXuLyAnhChiSo.CanChupLai)]
        [InlineData(TrangThaiXuLyAnhChiSo.MoiTaiLen)]
        [InlineData(TrangThaiXuLyAnhChiSo.DangXuLy)]
        public async Task ResetMeterBaseline_WithUnsuitableImageStatus_Fails(TrangThaiXuLyAnhChiSo status)
        {
            var (period, _) = SetupReset(status: status);

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m), 2);

            Assert.False(result.Success);
            Assert.Equal(4520m, period.ChiSoDienCu);
            Assert.False(_uow.CurrentTransaction.Committed);
        }

        [Theory]
        [InlineData(4520, 4600)]   // không nhỏ hơn chỉ số cũ
        [InlineData(4520, 100)]    // cũ chưa đủ gần mức quay vòng (< 90% của 10.000)
        public async Task ResetMeterBaseline_QuayVong_WithWrongCondition_Fails(int cu, int hienTai)
        {
            var (period, _) = SetupReset(dienCu: cu);

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, hienTai), 2);

            Assert.False(result.Success);
            Assert.Equal(cu, period.ChiSoDienCu);
        }

        [Fact]
        public async Task ResetMeterBaseline_QuayVong_SetsBaselineToZeroAndWritesNote()
        {
            var (period, image) = SetupReset(dienCu: 9950m);

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, 100m, lyDo: "Quay vòng"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((0m, 100m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal((50m, 50m), (period.ChiSoNuocCu, period.ChiSoNuocMoi));
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, image.TrangThaiXuLy);
            Assert.True(image.DuocChonLamChiSoChinhThuc);
            Assert.Equal("[QUAY_VONG] Chỉ số cũ 9.950; trần 10.000; phần trước mốc 50 kWh; lý do: Quay vòng", image.GhiChuXacNhan);
            Assert.True(_uow.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task ResetMeterBaseline_DoiDongHo_AcceptsNewNumberLargerThanOld_AndWritesNoteWithOldMeterPart()
        {
            var (period, image) = SetupReset(dienCu: 4520m);

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, soCuoi: 4565.25m, lyDo: "Thay đồng hồ"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((0m, 85.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal("[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 0; số cuối đồng hồ cũ 4.565,25; phần đồng hồ cũ 45,25 kWh; lý do: Thay đồng hồ", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_DoiDongHo_WithoutLastNumber_WritesShortNote()
        {
            var (_, image) = SetupReset(dienCu: 4520m);

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 4600m, soDau: 4500m, lyDo: "Thay"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal("[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 4.500; không có số cuối đồng hồ cũ; lý do: Thay", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_DoiDongHo_WithInvalidNumbers_Fails()
        {
            var (period, _) = SetupReset(dienCu: 4520m);

            var soDauLonHon = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 10m, soDau: 20m), 2);
            var soCuoiNhoHon = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85m, soCuoi: 4000m), 2);
            var khongLyDo = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85m, lyDo: "  "), 2);
            var lyDoDai = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85m, lyDo: new string('x', 501)), 2);
            var quaNhieuSoLe = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.1234m), 2);

            Assert.False(soDauLonHon.Success);
            Assert.False(soCuoiNhoHon.Success);
            Assert.False(khongLyDo.Success);
            Assert.False(lyDoDai.Success);
            Assert.False(quaNhieuSoLe.Success);
            Assert.Equal(4520m, period.ChiSoDienCu);
        }

        [Fact]
        public async Task ResetMeterBaseline_SecondTime_UsesPreviousPeriodNewReadingAsTrueOldNumber()
        {
            // Kỳ 09 kết thúc ở 4520. Kỳ 10 đã từng đổi mốc về 0 nên Cũ hiện là 0, chỉ số cũ thật vẫn là 4520.
            var prev = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 5, PhongTroId = 1, Thang = 9, Nam = 2026,
                ChiSoDienCu = 4400m, ChiSoDienMoi = 4520m, ChiSoNuocCu = 40m, ChiSoNuocMoi = 50m
            };
            var (period, image) = SetupReset(dienCu: 0m, others: new[] { prev });
            period.ChiSoDienMoi = 80m;

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, soCuoi: 4565.25m, lyDo: "Làm lại"), 2);

            Assert.True(result.Success, result.Message);
            Assert.StartsWith("[DOI_DONG_HO] Chỉ số cũ 4.520;", image.GhiChuXacNhan);
            Assert.Equal((0m, 85.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_RedoWithStartEqualToPreviousNew_RestoresNoBaselineChange()
        {
            var prev = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 5, PhongTroId = 1, Thang = 9, Nam = 2026,
                ChiSoDienCu = 4400m, ChiSoDienMoi = 4520m, ChiSoNuocCu = 40m, ChiSoNuocMoi = 50m
            };
            var (period, _) = SetupReset(dienCu: 0m, others: new[] { prev });
            period.ChiSoDienMoi = 85.5m;

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 4580m, soDau: 4520m, lyDo: "Đổi nhầm, hoàn tác"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(4520m, period.ChiSoDienCu);
            Assert.False(MeterBaselineChangePolicy.DaDoiMoc(period.ChiSoDienCu, prev.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_DropsOldOfficialImageOfSameType()
        {
            var (_, image) = SetupReset(dienCu: 9950m);
            var old = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 101, DichVuDienNuocCuaPhongId = 1, LoaiDongHo = LoaiDongHo.Dien, Url = "http://old",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan, DuocChonLamChiSoChinhThuc = true, GiaTriXacNhan = 9950m, NguoiXacNhanId = 1
            };
            _store.Images[101] = old;

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, 100m), 2);

            Assert.True(result.Success, result.Message);
            Assert.False(old.DuocChonLamChiSoChinhThuc);
            Assert.True(image.DuocChonLamChiSoChinhThuc);
        }

        [Fact]
        public async Task ResetMeterBaseline_ShiftsLaterDraftPeriodOldReading()
        {
            var next = LaterPeriod(2, 11, 4520m, 4520m, 50m, 50m);
            var (period, _) = SetupReset(dienCu: 4520m, others: new[] { next });

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((85.5m, 85.5m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal((50m, 50m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_WhenLaterPeriodAlreadyChangedBaseline_StopsAndRollsBack()
        {
            // Kỳ 11 đã đổi đồng hồ điện (Cũ 0 khác Mới 4520 của kỳ 10): không được ghi đè.
            var next = LaterPeriod(2, 11, 0m, 30m, 50m, 50m);
            var (period, _) = SetupReset(dienCu: 4520m, others: new[] { next });

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m), 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.Equal(0, _uow.SaveChangesCallCount);
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task ConfirmImageAsync_SuspectedExtraDigit_NeedsNote_ThenSucceedsWithNote()
        {
            var (period, _) = SetupReset(dienCu: 150m);

            var withoutNote = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 1500m }, 2);
            var withNote = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 1500m, GhiChu = "Đã đối chiếu đồng hồ, đúng 1500" }, 2);

            Assert.False(withoutNote.Success);
            Assert.Contains("bất thường", withoutNote.Message);
            Assert.True(withNote.Success, withNote.Message);
            Assert.Equal(1500m, period.ChiSoDienMoi);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenLaterPeriodChangedElectricBaseline_StopsAndKeepsLaterPeriod()
        {
            // Kỳ 11 có Cũ điện 0 (đã đổi đồng hồ) khác Mới điện kỳ 10 trước khi sửa (150).
            var next = LaterPeriod(2, 11, 0m, 30m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_ElectricEdit_IgnoresLaterPeriodWaterBaselineChange()
        {
            // Q1: kỳ 11 đổi đồng hồ nước (Cũ nước 0 khác Mới nước 70) nhưng sửa điện thì không bị dừng.
            var next = LaterPeriod(2, 11, 150m, 150m, 0m, 20m);
            SetupCorrectionWithLaterPeriods(out _, next);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((180m, 180m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal((0m, 20m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_Chain_UsesNewReadingBeforeTheEditForNextLink()
        {
            // Kỳ 11 có số riêng 200 (Cũ 150): Cũ chuyển sang 180, Mới giữ 200. Kỳ 12 Cũ 200 khớp Mới 200 trước sửa của kỳ 11
            // nên không còn gì để dịch (Mới kỳ 11 không đổi).
            var next1 = LaterPeriod(2, 11, 150m, 200m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 200m, 260m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next1, next2);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((180m, 200m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((200m, 260m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        // ---------- Bổ sung của Tester: cascade 5 nơi gọi, Q1, "chỉ số cũ thật", điều kiện quay vòng ----------

        private AnhChiSoDongHo AddWaterImage(int id = 101, TrangThaiXuLyAnhChiSo status = TrangThaiXuLyAnhChiSo.DocDuoc)
        {
            var image = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = id, DichVuDienNuocCuaPhongId = 1, LoaiDongHo = LoaiDongHo.Nuoc, Url = "http://test-nuoc",
                TrangThaiXuLy = status, GiaTriAIGoiY = 60m, DoTinCay = 0.95
            };
            _store.Images[id] = image;
            return image;
        }

        private static DichVuDienNuocCuaPhong PreviousPeriod(int id, int thang, decimal dienMoi, decimal nuocMoi, bool deleted = false) => new()
        {
            DichVuDienNuocCuaPhongId = id, PhongTroId = 1, Thang = thang, Nam = 2026,
            ChiSoDienCu = 0m, ChiSoDienMoi = dienMoi, ChiSoNuocCu = 0m, ChiSoNuocMoi = nuocMoi, IsDeleted = deleted
        };

        [Fact]
        public async Task ConfirmImageAsync_ShiftsEveryLaterDraftPeriodInTheChain()
        {
            var next1 = LaterPeriod(2, 11, 4520m, 4520m, 50m, 50m);
            var next2 = LaterPeriod(3, 12, 4520m, 4520m, 50m, 50m);
            var (period, _) = SetupReset(dienCu: 4520m, others: new[] { next1, next2 });

            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 4565m }, 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((4520m, 4565m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal((4565m, 4565m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((4565m, 4565m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
            Assert.Equal((50m, 50m), (next1.ChiSoNuocCu, next1.ChiSoNuocMoi));
            Assert.True(_uow.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task ConfirmImageAsync_WhenLaterPeriodChangedElectricBaseline_StopsWithoutCommit()
        {
            var next = LaterPeriod(2, 11, 0m, 30m, 50m, 50m);
            var (period, image) = SetupReset(dienCu: 4520m, others: new[] { next });

            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 100, GiaTriXacNhan = 4565m }, 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.Equal(0, _uow.SaveChangesCallCount);
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task ConfirmImageAsync_WaterImage_IgnoresLaterElectricBaselineChange_AndShiftsWater()
        {
            // Q1: kỳ 11 đã đổi đồng hồ điện (Cũ 0 khác Mới 4520), nhưng ảnh nước chỉ làm đổi số nước.
            var next = LaterPeriod(2, 11, 0m, 30m, 50m, 50m);
            var (period, _) = SetupReset(dienCu: 4520m, others: new[] { next });
            AddWaterImage();

            var result = await _service.ConfirmImageAsync(new ConfirmMeterImageRequest { AnhChiSoDongHoId = 101, GiaTriXacNhan = 60m }, 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((60m, 60m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_WhenSecondLinkChangedBaseline_FailsAndRollsBackWithoutSaving()
        {
            // Kỳ 11 còn nháp (Cũ 150 = Mới kỳ 10) nên bị dịch; kỳ 12 có Cũ 0 khác Mới 150 của kỳ 11 trước khi dịch.
            var next1 = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 0m, 40m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next1, next2);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.False(result.Success);
            Assert.Contains("12/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            Assert.Equal(0, _uow.SaveChangesCallCount);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.Equal((0m, 40m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_ChainStopsWhereNewReadingStaysTheSame_EvenIfFurtherPeriodChangedBaseline()
        {
            // Kỳ 11 có số riêng 200: Mới của nó không đổi nên kỳ 12 (Cũ 0, đã đổi mốc) không liên quan và không bị dừng.
            var next1 = LaterPeriod(2, 11, 150m, 200m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 0m, 40m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next1, next2);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((180m, 200m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((0m, 40m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        [Fact]
        public async Task CorrectConfirmedImageAsync_MixedChain_PlaceholderThenOwnReading()
        {
            var next1 = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 150m, 260m, 70m, 70m);
            SetupCorrectionWithLaterPeriods(out _, next1, next2);

            var result = await _service.CorrectConfirmedImageAsync(new CorrectConfirmedMeterImageRequest(100, 180m, "Sửa số"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((180m, 180m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((180m, 260m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        // ----- AdjustReadingsInCurrentTransactionAsync (điều chỉnh hóa đơn) -----

        private DichVuDienNuocCuaPhong SetupAdjust(params DichVuDienNuocCuaPhong[] later)
        {
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1, PhongTroId = 1, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 150m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 70m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            _store.Periods[1] = period;
            _store.RoomBranches[1] = 10;
            foreach (var l in later) _store.Periods[l.DichVuDienNuocCuaPhongId] = l;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));
            return period;
        }

        private static MeterAdjustmentRequest Adjust(decimal dien, decimal nuoc) => new()
        {
            DichVuDienNuocCuaPhongId = 1, ChiSoDienMoi = dien, ChiSoNuocMoi = nuoc, LyDo = "Khách yêu cầu sửa"
        };

        [Fact]
        public async Task Adjust_ShiftsAllLaterDraftPeriods_UsingNewReadingBeforeTheEdit()
        {
            var next1 = LaterPeriod(2, 11, 150m, 150m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 150m, 150m, 70m, 70m);
            var period = SetupAdjust(next1, next2);

            var result = await _service.AdjustReadingsInCurrentTransactionAsync(Adjust(180m, 70m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((150m, 180m), (result.Data!.ChiSoDienCu, result.Data.ChiSoDienMoi));
            Assert.Equal((180m, 180m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((180m, 180m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
            Assert.Equal((70m, 70m), (next1.ChiSoNuocCu, next1.ChiSoNuocMoi));
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
        }

        [Fact]
        public async Task Adjust_ChainEndsAtLaterPeriodWithOwnReading()
        {
            var next1 = LaterPeriod(2, 11, 150m, 200m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 200m, 260m, 70m, 70m);
            SetupAdjust(next1, next2);

            var result = await _service.AdjustReadingsInCurrentTransactionAsync(Adjust(180m, 70m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((180m, 200m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((200m, 260m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        [Fact]
        public async Task Adjust_WhenLaterPeriodChangedElectricBaseline_StopsWithoutSaving()
        {
            var next = LaterPeriod(2, 11, 0m, 30m, 70m, 70m);
            SetupAdjust(next);

            var result = await _service.AdjustReadingsInCurrentTransactionAsync(Adjust(180m, 70m), 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            Assert.Equal(0, _uow.SaveChangesCallCount);
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task Adjust_WaterOnly_IgnoresLaterElectricBaselineChange()
        {
            // Q1: chỉ sửa nước; kỳ sau đổi đồng hồ điện không cản.
            var next = LaterPeriod(2, 11, 0m, 30m, 70m, 70m);
            SetupAdjust(next);

            var result = await _service.AdjustReadingsInCurrentTransactionAsync(Adjust(150m, 75m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((75m, 75m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task Adjust_ElectricAndWaterChainsAreIndependent()
        {
            // Điện: kỳ 11 có số riêng nên chuỗi điện dừng ở đó. Nước: cả hai kỳ sau còn nháp nên cùng dịch.
            var next1 = LaterPeriod(2, 11, 150m, 200m, 70m, 70m);
            var next2 = LaterPeriod(3, 12, 200m, 260m, 70m, 70m);
            SetupAdjust(next1, next2);

            var result = await _service.AdjustReadingsInCurrentTransactionAsync(Adjust(180m, 75m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((180m, 200m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((200m, 260m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
            Assert.Equal((75m, 75m), (next1.ChiSoNuocCu, next1.ChiSoNuocMoi));
            Assert.Equal((75m, 75m), (next2.ChiSoNuocCu, next2.ChiSoNuocMoi));
        }

        // ----- ApprovePeriodsAsync nhiều phòng -----

        private (DichVuDienNuocCuaPhong P1, DichVuDienNuocCuaPhong L1, DichVuDienNuocCuaPhong L1b, DichVuDienNuocCuaPhong P2, DichVuDienNuocCuaPhong L2) SetupTwoRoomApproval(
            DichVuDienNuocCuaPhong? room2Later = null)
        {
            DichVuDienNuocCuaPhong Draft(int id, int room, int thang) => new()
            {
                DichVuDienNuocCuaPhongId = id, PhongTroId = room, Thang = thang, Nam = 2026,
                ChiSoDienCu = 100m, ChiSoDienMoi = 100m, ChiSoNuocCu = 50m, ChiSoNuocMoi = 50m, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };

            var p1 = Draft(1, 1, 10);
            var l1 = Draft(2, 1, 11);
            var l1b = Draft(5, 1, 12);
            var p2 = Draft(3, 2, 10);
            var l2 = room2Later ?? Draft(4, 2, 11);
            foreach (var p in new[] { p1, l1, l1b, p2, l2 }) _store.Periods[p.DichVuDienNuocCuaPhongId] = p;
            _store.RoomBranches[1] = 10;
            _store.RoomBranches[2] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));
            return (p1, l1, l1b, p2, l2);
        }

        private static ApproveMeterPeriodsRequest ApproveBoth(decimal dien2, decimal nuoc2) => new()
        {
            Thang = 10,
            Nam = 2026,
            DanhSachPhong = new List<ApproveMeterPeriodItem> { ManualApproveItem(1, 160m, 65m), ManualApproveItem(2, dien2, nuoc2) }
        };

        [Fact]
        public async Task ApprovePeriodsAsync_TwoRooms_ShiftsLaterDraftChainOfEveryRoom()
        {
            var (_, l1, l1b, _, l2) = SetupTwoRoomApproval();

            var result = await _service.ApprovePeriodsAsync(ApproveBoth(150m, 55m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((160m, 160m, 65m, 65m), (l1.ChiSoDienCu, l1.ChiSoDienMoi, l1.ChiSoNuocCu, l1.ChiSoNuocMoi));
            Assert.Equal((160m, 160m, 65m, 65m), (l1b.ChiSoDienCu, l1b.ChiSoDienMoi, l1b.ChiSoNuocCu, l1b.ChiSoNuocMoi));
            Assert.Equal((150m, 150m, 55m, 55m), (l2.ChiSoDienCu, l2.ChiSoDienMoi, l2.ChiSoNuocCu, l2.ChiSoNuocMoi));
            Assert.True(_uow.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task ApprovePeriodsAsync_SecondRoomLaterPeriodChangedBaseline_RejectsWholeBatchWithRoomInMessage()
        {
            var changed = LaterPeriod(4, 11, 0m, 30m, 50m, 50m);
            changed.PhongTroId = 2;
            var (_, _, _, _, l2) = SetupTwoRoomApproval(changed);

            var result = await _service.ApprovePeriodsAsync(ApproveBoth(150m, 55m), 2);

            Assert.False(result.Success);
            Assert.Contains("Phòng 2", result.Message);
            Assert.Contains("11/2026", result.Message);
            Assert.Contains("đã đổi đồng hồ hoặc quay vòng", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.Equal((0m, 30m), (l2.ChiSoDienCu, l2.ChiSoDienMoi));
        }

        [Fact]
        public async Task ApprovePeriodsAsync_WaterUnchanged_IgnoresLaterWaterBaselineChange()
        {
            // Q1: phòng 2 duyệt nước đúng bằng số hiện có (50 → 50) nên kỳ sau đã đổi đồng hồ nước không cản; điện vẫn dịch.
            var changed = LaterPeriod(4, 11, 100m, 100m, 0m, 20m);
            changed.PhongTroId = 2;
            var (_, _, _, _, l2) = SetupTwoRoomApproval(changed);

            var result = await _service.ApprovePeriodsAsync(ApproveBoth(150m, 50m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((150m, 150m), (l2.ChiSoDienCu, l2.ChiSoDienMoi));
            Assert.Equal((0m, 20m), (l2.ChiSoNuocCu, l2.ChiSoNuocMoi));
        }

        // ----- ResetMeterBaselineAsync: điều kiện quay vòng, "chỉ số cũ thật", ghi chú, cascade -----

        [Theory]
        [InlineData(9000, true)]       // đúng 90% của 10.000
        [InlineData(8999.999, false)]  // dưới 90%
        [InlineData(950, false)]       // trần tối thiểu 10.000 nên 950 chỉ là 9,5% (không phải 95% của 1.000)
        [InlineData(10_000, false)]    // trần là 100.000, 10.000 chỉ là 10%
        [InlineData(90_000, true)]     // đúng 90% của 100.000
        public async Task ResetMeterBaseline_QuayVong_NinetyPercentOfCeilingBoundary(double cu, bool allowed)
        {
            var (period, _) = SetupReset(dienCu: (decimal)cu);

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, 100m), 2);

            Assert.Equal(allowed, result.Success);
            Assert.Equal(allowed ? 0m : (decimal)cu, period.ChiSoDienCu);
        }

        [Fact]
        public async Task ResetMeterBaseline_QuayVong_FiveDigitCeiling_WritesCeilingAndPartBeforeBaseline()
        {
            var (period, image) = SetupReset(dienCu: 95_000m);

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, 100m, lyDo: "Quay vòng"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((0m, 100m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal("[QUAY_VONG] Chỉ số cũ 95.000; trần 100.000; phần trước mốc 5.000 kWh; lý do: Quay vòng", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_QuayVong_Water_UsesCubicMetersAndKeepsElectricity()
        {
            var (period, _) = SetupReset(dienCu: 4520m);
            period.ChiSoNuocCu = 9990m;
            period.ChiSoNuocMoi = 9990m;
            var image = AddWaterImage();

            var result = await _service.ResetMeterBaselineAsync(
                new ResetMeterBaselineRequest(101, MeterBaselineResetType.QuayVong, 3m, null, null, "Quay vòng nước"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((0m, 3m), (period.ChiSoNuocCu, period.ChiSoNuocMoi));
            Assert.Equal((4520m, 4520m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal("[QUAY_VONG] Chỉ số cũ 9.990; trần 10.000; phần trước mốc 10 m³; lý do: Quay vòng nước", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_QuayVong_IgnoresStartAndLastNumbersFromRequest()
        {
            var (period, image) = SetupReset(dienCu: 9950m);

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.QuayVong, 100m, soDau: 77m, soCuoi: 9999m, lyDo: "Quay vòng"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(0m, period.ChiSoDienCu);
            Assert.StartsWith("[QUAY_VONG]", image.GhiChuXacNhan);
            Assert.DoesNotContain("số cuối", image.GhiChuXacNhan);
            Assert.DoesNotContain("số đầu", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_SecondTime_QuayVongConditionUsesTrueOldNumberNotCurrentBaseline()
        {
            // Kỳ 09 kết thúc ở 9950. Kỳ 10 từng đổi mốc về 0 (Cũ hiện là 0): quay vòng vẫn hợp lệ theo chỉ số cũ thật 9950.
            var prev = PreviousPeriod(5, 9, dienMoi: 9950m, nuocMoi: 50m);
            var (period, image) = SetupReset(dienCu: 0m, others: new[] { prev });
            period.ChiSoDienMoi = 80m;

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, 100m, lyDo: "Làm lại"), 2);

            Assert.True(result.Success, result.Message);
            Assert.StartsWith("[QUAY_VONG] Chỉ số cũ 9.950; trần 10.000;", image.GhiChuXacNhan);
            Assert.Equal((0m, 100m), (period.ChiSoDienCu, period.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_SecondTime_LastNumberIsComparedWithTrueOldNumber()
        {
            // Nếu dùng Cũ hiện tại (0) thì số cuối 4000 hợp lệ; dùng chỉ số cũ thật 4520 thì phải bị từ chối.
            var prev = PreviousPeriod(5, 9, dienMoi: 4520m, nuocMoi: 50m);
            var (period, _) = SetupReset(dienCu: 0m, others: new[] { prev });
            period.ChiSoDienMoi = 80m;

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, soCuoi: 4000m), 2);

            Assert.False(result.Success);
            Assert.Contains("4.520", result.Message);
            Assert.Equal((0m, 80m), (period.ChiSoDienCu, period.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_PreviousPeriodSoftDeleted_UsesNearestUndeletedPreviousPeriod()
        {
            var deleted = PreviousPeriod(6, 9, dienMoi: 9999m, nuocMoi: 50m, deleted: true);
            var older = PreviousPeriod(5, 8, dienMoi: 4520m, nuocMoi: 50m);
            var (_, image) = SetupReset(dienCu: 7m, others: new[] { deleted, older });

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, soCuoi: 4565.25m), 2);

            Assert.True(result.Success, result.Message);
            Assert.StartsWith("[DOI_DONG_HO] Chỉ số cũ 4.520;", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_DoiDongHo_BoundaryValuesAreAccepted()
        {
            // Số cuối bằng chỉ số cũ (phần đồng hồ cũ 0) và số đầu bằng số hiện tại đều hợp lệ.
            var (period, image) = SetupReset(dienCu: 4520m);

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 12.5m, soDau: 12.5m, soCuoi: 4520m, lyDo: "Thay đồng hồ"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((12.5m, 12.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.Equal("[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 12,5; số cuối đồng hồ cũ 4.520; phần đồng hồ cũ 0 kWh; lý do: Thay đồng hồ", image.GhiChuXacNhan);
        }

        [Fact]
        public async Task ResetMeterBaseline_DoiDongHo_NegativeNumbers_Fail()
        {
            var (period, _) = SetupReset(dienCu: 4520m);

            var negativeStart = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85m, soDau: -1m), 2);
            var negativeCurrent = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, -1m), 2);
            var negativeLast = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85m, soCuoi: -1m), 2);

            Assert.False(negativeStart.Success);
            Assert.False(negativeCurrent.Success);
            Assert.False(negativeLast.Success);
            Assert.Equal((4520m, 4520m), (period.ChiSoDienCu, period.ChiSoDienMoi));
            Assert.False(_uow.CurrentTransaction.Committed);
        }

        [Fact]
        public async Task ResetMeterBaseline_Reason_ExactlyLimit_SucceedsAndNoteStaysWithinColumnLimit()
        {
            var (_, image) = SetupReset(dienCu: 4520m);
            var reason = new string('x', 500);

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, soCuoi: 4565.25m, lyDo: "  " + reason + "  "), 2);

            Assert.True(result.Success, result.Message);
            Assert.True(image.GhiChuXacNhan!.Length <= 1000);
            Assert.EndsWith("lý do: " + reason, image.GhiChuXacNhan);
        }

        [Theory]
        [InlineData(TrangThaiXuLyAnhChiSo.KhongDocDuoc)]
        [InlineData(TrangThaiXuLyAnhChiSo.Loi)]
        public async Task ResetMeterBaseline_OnUnreadableImage_ConfirmsManuallyWithServerNote(TrangThaiXuLyAnhChiSo status)
        {
            var (period, image) = SetupReset(dienCu: 4520m, status: status);

            var result = await _service.ResetMeterBaselineAsync(
                Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, lyDo: "Ảnh mờ, đọc tay"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiXuLyAnhChiSo.DaXacNhan, image.TrangThaiXuLy);
            Assert.True(image.DuocChonLamChiSoChinhThuc);
            Assert.Equal(85.5m, image.GiaTriXacNhan);
            Assert.Equal((0m, 85.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_OnApprovedPeriod_StaysApprovedAndRecordsReviewer()
        {
            var (period, _) = SetupReset(dienCu: 4520m);
            period.TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet;
            period.NguoiDuyetId = 1;

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiGhiNhan.DaDuyet, period.TrangThaiGhiNhan);
            Assert.Equal(2, period.NguoiDuyetId);
            Assert.Equal((0m, 85.5m), (period.ChiSoDienCu, period.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_ShiftsEveryLaterDraftPeriodInTheChain()
        {
            var next1 = LaterPeriod(2, 11, 4520m, 4520m, 50m, 50m);
            var next2 = LaterPeriod(3, 12, 4520m, 4520m, 50m, 50m);
            SetupReset(dienCu: 4520m, others: new[] { next1, next2 });

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((85.5m, 85.5m), (next1.ChiSoDienCu, next1.ChiSoDienMoi));
            Assert.Equal((85.5m, 85.5m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_WhenSecondLinkChangedBaseline_StopsAndRollsBack()
        {
            var next1 = LaterPeriod(2, 11, 4520m, 4520m, 50m, 50m);
            var next2 = LaterPeriod(3, 12, 0m, 30m, 50m, 50m);
            SetupReset(dienCu: 4520m, others: new[] { next1, next2 });

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m), 2);

            Assert.False(result.Success);
            Assert.Contains("12/2026", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
            Assert.Equal((0m, 30m), (next2.ChiSoDienCu, next2.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_Electric_IgnoresLaterWaterBaselineChange()
        {
            // Q1: đổi đồng hồ điện, kỳ sau đã đổi đồng hồ nước: không cản và không đụng số nước.
            var next = LaterPeriod(2, 11, 4520m, 4520m, 0m, 20m);
            SetupReset(dienCu: 4520m, others: new[] { next });

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((85.5m, 85.5m), (next.ChiSoDienCu, next.ChiSoDienMoi));
            Assert.Equal((0m, 20m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_Water_ShiftsOnlyWaterOfLaterPeriod()
        {
            var next = LaterPeriod(2, 11, 0m, 30m, 50m, 50m);
            SetupReset(dienCu: 4520m, others: new[] { next });
            AddWaterImage();

            var result = await _service.ResetMeterBaselineAsync(
                new ResetMeterBaselineRequest(101, MeterBaselineResetType.DoiDongHo, 3m, 0m, 70m, "Thay đồng hồ nước"), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((3m, 3m), (next.ChiSoNuocCu, next.ChiSoNuocMoi));
            Assert.Equal((0m, 30m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_Again_CascadeUsesNewReadingBeforeTheRedo_NotTheTrueOldNumber()
        {
            // Lần một đã đưa kỳ 10 về (0, 80) và kỳ 11 về (80, 80). Làm lại lần hai: kỳ 11 phải so với 80, không phải 4520.
            var prev = PreviousPeriod(5, 9, dienMoi: 4520m, nuocMoi: 50m);
            var next = LaterPeriod(2, 11, 80m, 80m, 50m, 50m);
            var (period, _) = SetupReset(dienCu: 0m, others: new[] { prev, next });
            period.ChiSoDienMoi = 80m;

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.DoiDongHo, 85.5m, soDau: 0m, soCuoi: 4565.25m), 2);

            Assert.True(result.Success, result.Message);
            Assert.Equal((85.5m, 85.5m), (next.ChiSoDienCu, next.ChiSoDienMoi));
        }

        [Fact]
        public async Task ResetMeterBaseline_WhenCascadeFailsAfterDroppingOldOfficialImage_RollsBackTransaction()
        {
            // Bỏ ảnh chính thức cũ cần một lần SaveChanges giữa chừng (tránh vi phạm unique); cascade lỗi sau đó thì transaction phải rollback.
            var next = LaterPeriod(2, 11, 0m, 30m, 50m, 50m);
            SetupReset(dienCu: 9950m, others: new[] { next });
            var old = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 101, DichVuDienNuocCuaPhongId = 1, LoaiDongHo = LoaiDongHo.Dien, Url = "http://old",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan, DuocChonLamChiSoChinhThuc = true, GiaTriXacNhan = 9950m, NguoiXacNhanId = 1
            };
            _store.Images[101] = old;

            var result = await _service.ResetMeterBaselineAsync(Reset(MeterBaselineResetType.QuayVong, 100m), 2);

            Assert.False(result.Success);
            Assert.Contains("11/2026", result.Message);
            Assert.True(_uow.CurrentTransaction.RolledBack);
            Assert.False(_uow.CurrentTransaction.Committed);
        }

        #endregion

        #region Task 2.6 Tests

        [Fact]
        public async Task Upload_FuturePeriod_Rejected_NoStorageCall()
        {
            _timeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 15, 3, 0, 0, TimeSpan.Zero));
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            var req = CreateValidRequest(); // Thang = 10, Nam = 2026
            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Equal("Không thể gửi ảnh cho kỳ chưa tới.", result.Message);
            Assert.Equal(0, _storage.UploadCallCount);
            Assert.Empty(_store.Images);
            Assert.Empty(_store.Periods);
        }

        [Fact]
        public async Task Upload_PreviousPeriodNotApproved_Rejected()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _store.ContractMonths.Add((1, 9, 2026));
            _store.Periods[99] = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 99,
                PhongTroId = 1,
                Thang = 9,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };

            var req = CreateValidRequest(); // Thang = 10, Nam = 2026
            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Equal("Kỳ 09/2026 của phòng chưa được chốt, chưa thể gửi ảnh kỳ 10/2026.", result.Message);
            Assert.Null(_store.Periods.Values.FirstOrDefault(p => p.Thang == 10 && p.Nam == 2026));
        }

        [Fact]
        public async Task Upload_PreviousPeriodMissing_WithContract_Rejected()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _store.ContractMonths.Add((1, 9, 2026)); // có hợp đồng tháng 9 nhưng không có bản ghi kỳ 9

            var req = CreateValidRequest(); // Thang = 10, Nam = 2026
            var result = await _service.UploadImageAsync(req, 2);

            Assert.False(result.Success);
            Assert.Equal("Kỳ 09/2026 của phòng chưa được chốt, chưa thể gửi ảnh kỳ 10/2026.", result.Message);
        }

        [Fact]
        public async Task Upload_NoContractPreviousMonth_Allowed()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            // Không có hợp đồng tháng 9

            var req = CreateValidRequest(); // Thang = 10, Nam = 2026
            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.NotNull(_store.Periods.Values.FirstOrDefault(p => p.Thang == 10 && p.Nam == 2026));
        }

        [Fact]
        public async Task Upload_PreviousPeriodApproved_Allowed()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _store.ContractMonths.Add((1, 9, 2026));
            _store.Periods[99] = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 99,
                PhongTroId = 1,
                Thang = 9,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Upload_Tenant_TwoMonthsAgo_Rejected()
        {
            _store.RoomBranches[1] = 10;
            _store.ActiveTenantContracts.Add((1, 99, 8, 2026));

            var req = CreateValidRequest();
            req.Thang = 8;
            req.Nam = 2026;

            var result = await _service.UploadImageAsync(req, 99);

            Assert.False(result.Success);
            Assert.Equal("Khách thuê chỉ được gửi ảnh cho tháng hiện tại hoặc tháng trước.", result.Message);
        }

        [Fact]
        public async Task Upload_Staff_TwoMonthsAgo_AllowedIfOtherRulesPass()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));

            var req = CreateValidRequest();
            req.Thang = 8;
            req.Nam = 2026;

            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Upload_Tenant_PeriodApproved_Rejected()
        {
            _store.RoomBranches[1] = 10;
            _store.ActiveTenantContracts.Add((1, 99, 10, 2026));
            _store.Periods[1] = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 99);

            Assert.False(result.Success);
            Assert.Equal("Kỳ này đã được chốt, không thể gửi thêm ảnh.", result.Message);
        }

        [Fact]
        public async Task Upload_Staff_PeriodApproved_Allowed()
        {
            _store.RoomBranches[1] = 10;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterUpload));
            _store.Periods[1] = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 2);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Upload_Tenant_PeriodApprovedDuringTransaction_RejectedAndStorageCleaned()
        {
            _store.RoomBranches[1] = 10;
            _store.ActiveTenantContracts.Add((1, 99, 10, 2026));
            _store.Periods[1] = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };

            // Fake trả Nhap ở lần đọc sơ bộ, nhưng DaDuyet ở GetPeriodForUpdateAsync
            _store.PeriodForUpdateOverride = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };

            var req = CreateValidRequest();
            var result = await _service.UploadImageAsync(req, 99);

            Assert.False(result.Success);
            Assert.Equal("Kỳ này đã được chốt, không thể gửi thêm ảnh.", result.Message);
            Assert.Single(_storage.DeletedPublicIds);
            Assert.Equal("meters/img1", _storage.DeletedPublicIds[0]);
        }

        [Fact]
        public async Task Confirm_OnApprovedPeriod_CallsDuyetLai()
        {
            _store.RoomBranches[1] = 10;
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                NguoiDuyetId = 5,
                GhiChuDuyet = "Duyet cu",
                NgayDuyet = DateTime.UtcNow.AddDays(-2),
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 140m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m
            };
            _store.Periods[1] = period;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 150m
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest
            {
                AnhChiSoDongHoId = 100,
                GiaTriXacNhan = 150m,
                GhiChu = "Duyet lai thang 10"
            };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.Equal(2, period.NguoiDuyetId);
            Assert.Equal("Duyet cu", period.GhiChuDuyet);
            Assert.True(period.NgayDuyet > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task Confirm_OnDraftPeriod_DoesNotChangeApprovalFields()
        {
            _store.RoomBranches[1] = 10;
            var period = new DichVuDienNuocCuaPhong
            {
                DichVuDienNuocCuaPhongId = 1,
                PhongTroId = 1,
                Thang = 10,
                Nam = 2026,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap,
                NguoiDuyetId = null,
                GhiChuDuyet = null,
                NgayDuyet = null,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 140m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m
            };
            _store.Periods[1] = period;

            var img = new AnhChiSoDongHo
            {
                AnhChiSoDongHoId = 100,
                DichVuDienNuocCuaPhongId = 1,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://test",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 150m
            };
            _store.Images[100] = img;
            _access.Permissions.Add((2, 10, EmployeeActionCodes.MeterReview));

            var req = new ConfirmMeterImageRequest
            {
                AnhChiSoDongHoId = 100,
                GiaTriXacNhan = 150m,
                GhiChu = "Xac nhan ky nhap"
            };
            var result = await _service.ConfirmImageAsync(req, 2);

            Assert.True(result.Success);
            Assert.Null(period.NguoiDuyetId);
            Assert.Null(period.NgayDuyet);
        }

        #endregion
    }
}
