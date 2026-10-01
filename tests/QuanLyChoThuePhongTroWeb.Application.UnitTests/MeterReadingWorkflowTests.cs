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

            public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
            {
                UploadCallCount++;
                if (ThrowOnUpload) throw new InvalidOperationException("Upload storage failed");
                OnUpload?.Invoke();
                return Task.FromResult(new MeterImageUploadResult("https://storage.test/img1.jpg", "meters/img1"));
            }

            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
            {
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

            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
            {
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
                    nullIndex == 8 ? null! : options
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
                ChiSoDienCu = 100m, ChiSoNuocCu = 50m, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
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
                ChiSoDienCu = 100m, ChiSoNuocCu = 50m, TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
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
