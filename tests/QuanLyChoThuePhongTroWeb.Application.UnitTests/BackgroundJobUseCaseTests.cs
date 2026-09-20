using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class BackgroundJobUseCaseTests
    {
        private class FakeInvoiceReminderStateStore : IInvoiceReminderStateStore
        {
            public HashSet<int> SentIds { get; } = new();
            public bool ThrowOnMark { get; set; }

            public Task<IReadOnlySet<int>> GetSentInvoiceIdsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlySet<int>>(SentIds);
            }

            public Task<bool> HasBeenSentAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SentIds.Contains(hoaDonId));
            }

            public Task MarkAsSentAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                if (ThrowOnMark)
                {
                    throw new System.IO.IOException("Simulated disk write failure");
                }
                SentIds.Add(hoaDonId);
                return Task.CompletedTask;
            }
        }

        private class FakeContractExpiryAlertStateStore : IContractExpiryAlertStateStore
        {
            public HashSet<string> SentKeys { get; } = new();
            public bool ThrowOnMark { get; set; }

            public Task<IReadOnlySet<string>> GetSentAlertKeysAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlySet<string>>(SentKeys);
            }

            public Task<bool> HasBeenSentAsync(string alertKey, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SentKeys.Contains(alertKey));
            }

            public Task MarkAsSentAsync(string alertKey, CancellationToken cancellationToken = default)
            {
                if (ThrowOnMark)
                {
                    throw new System.IO.IOException("Simulated disk write failure");
                }
                SentKeys.Add(alertKey);
                return Task.CompletedTask;
            }
        }

        private class FakeHoaDonStore : IHoaDonStore
        {
            public List<HoaDon> Invoices { get; set; } = new();

            public Task<IReadOnlyList<HoaDon>> GetOverdueInvoicesAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<HoaDon>>(Invoices);
            }

            public Task<ChiNhanh?> GetChiNhanhByIdAsync(int chiNhanhId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetValidContractsForBillingAsync(int chiNhanhId, IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<int>> GetExistingInvoiceContractIdsAsync(IReadOnlyList<int> contractIds, int thang, int nam, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetDichVuDienNuocByRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DangKyDichVu>> GetDangKyDichVusForBillingAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableSuCosAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<PhongTro>> GetPhongTrosByChiNhanhIdAsync(int chiNhanhId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HoaDon?> GetHoaDonWithDetailsForUpdateAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetHoaDonsDataTableAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HoaDonChiTietRes?> GetHoaDonDetailByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HoaDon?> GetActiveHoaDonByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HoaDonRes>> GetUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<int>> GetContractIdsByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HoaDonRes>> GetInvoicesByContractIdsAsync(IReadOnlyList<int> contractIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddInvoicesAsync(IEnumerable<HoaDon> invoices, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public void UpdateSuCos(IEnumerable<YeuCauSuCo> suCos) => throw new NotImplementedException();
            public void RemoveChiTietHoaDons(IEnumerable<ChiTietHoaDon> chiTiets) => throw new NotImplementedException();
            public void UpdateHoaDon(HoaDon hoaDon) => throw new NotImplementedException();
            public Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class FakeHopDongStore : IHopDongStore
        {
            public List<HopDong> Contracts { get; set; } = new();

            public Task<IReadOnlyList<HopDong>> GetActiveExpiringContractsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<HopDong>>(Contracts);
            }

            public Task<DataTableResponse<HopDongRes>> GetDataTableResponseAsync(HopDongFilterReq request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongDetailRes?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDong?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDong?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<string?> GetMaChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<int> CountContractsWithPrefixAsync(string prefix, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> IsRoomRentedAsync(int phongTroId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int? excludeHopDongId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<string>> GetOverlappingLivingMemberNamesAsync(IReadOnlyList<int> memberIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<NguoiThue?> GetNguoiThueByIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> ExistsActiveAccountForTenantAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermsByIdsAsync(IReadOnlyList<int> termIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> HasInvoicesAsync(int hopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDongDieuKhoan>> GetHopDongDieuKhoansByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongPrintRes?> GetPrintDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesByRoomIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddAsync(HopDong hopDong, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddNguoiDungAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public void Update(HopDong hopDong) => throw new NotImplementedException();
            public void UpdatePhongTro(PhongTro phongTro) => throw new NotImplementedException();
            public void RemoveTerms(IEnumerable<HopDongDieuKhoan> terms) => throw new NotImplementedException();
        }

        private class FakeEmailService : IEmailService
        {
            public bool ReturnSuccess { get; set; } = true;
            public int SentCount { get; private set; }

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                if (ReturnSuccess)
                {
                    SentCount++;
                    return Task.FromResult((true, string.Empty));
                }
                return Task.FromResult((false, "Email sending failed"));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
            {
                if (ReturnSuccess)
                {
                    SentCount++;
                    return Task.FromResult((true, string.Empty));
                }
                return Task.FromResult((false, "Email sending failed"));
            }
        }

        private class FakeHoaDonService : IHoaDonService
        {
            public Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id)
            {
                return Task.FromResult<HoaDonChiTietRes?>(new HoaDonChiTietRes
                {
                    HoaDonId = id,
                    MaHoaDon = "HD-" + id
                });
            }

            public Task<byte[]?> ExportPdfAsync(int id)
            {
                return Task.FromResult<byte[]?>(new byte[] { 1, 2, 3, 4 });
            }

            public Task<PhatSinhHoaDonResult> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, List<int> selectedPhongTroIds) => throw new NotImplementedException();
            public Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> DeleteHoaDonAsync(int id) => throw new NotImplementedException();
            public Task<byte[]?> ExportExcelAsync(int hoaDonId) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId) => throw new NotImplementedException();
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId) => throw new NotImplementedException();
        }

        [Fact]
        public async Task InvoiceReminder_FirstDelivery_ShouldSendEmailAndMarkState()
        {
            // Arrange
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 101,
                        MaHoaDon = "HD-101",
                        HopDong = new HopDong
                        {
                            NguoiThue = new NguoiThue { Email = "tenant1@test.com" }
                        }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore();
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            // Act
            var count = await useCase.ExecuteAsync();

            // Assert
            Assert.Equal(1, count);
            Assert.Equal(1, emailService.SentCount);
            Assert.True(await stateStore.HasBeenSentAsync(101));
        }

        [Fact]
        public async Task InvoiceReminder_DuplicateSuppression_ShouldSkipAlreadySent()
        {
            // Arrange
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 101,
                        MaHoaDon = "HD-101",
                        HopDong = new HopDong
                        {
                            NguoiThue = new NguoiThue { Email = "tenant1@test.com" }
                        }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore();
            await stateStore.MarkAsSentAsync(101); // Pre-marked as sent
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            // Act
            var count = await useCase.ExecuteAsync();

            // Assert
            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_FailedDelivery_ShouldNotMarkState()
        {
            // Arrange
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 102,
                        MaHoaDon = "HD-102",
                        HopDong = new HopDong
                        {
                            NguoiThue = new NguoiThue { Email = "tenant2@test.com" }
                        }
                    }
                }
            };
            var emailService = new FakeEmailService { ReturnSuccess = false };
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore();
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            // Act
            var count = await useCase.ExecuteAsync();

            // Assert
            Assert.Equal(0, count);
            Assert.False(await stateStore.HasBeenSentAsync(102));
        }

        [Fact]
        public async Task InvoiceReminder_CancellationPropagation_ShouldStopProcessing()
        {
            // Arrange
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 103,
                        MaHoaDon = "HD-103",
                        HopDong = new HopDong
                        {
                            NguoiThue = new NguoiThue { Email = "tenant3@test.com" }
                        }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore();
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Already cancelled

            // Act
            var count = await useCase.ExecuteAsync(cts.Token);

            // Assert
            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task ContractExpiryAlert_FirstDelivery_ShouldSendAlertAndMarkState()
        {
            // Arrange
            var now = DateTime.UtcNow.Date;
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 201,
                        MaHopDong = "HDONG-201",
                        ThoiDiemKetThuc = now.AddDays(30),
                        NguoiThue = new NguoiThue { HoVaTen = "Nguyễn Văn A", Email = "a@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var stateStore = new FakeContractExpiryAlertStateStore();
            var settings = new ContractAlertSettings { Enabled = true, AlertDays = new List<int> { 30, 15 }, AdminEmail = null };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            // Act
            var count = await useCase.ExecuteAsync();

            // Assert
            Assert.Equal(1, count);
            Assert.Equal(1, emailService.SentCount);
            Assert.True(await stateStore.HasBeenSentAsync("201_30"));
        }

        [Fact]
        public async Task ContractExpiryAlert_DuplicateSuppression_ShouldSkipAlreadySent()
        {
            // Arrange
            var now = DateTime.UtcNow.Date;
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 201,
                        MaHopDong = "HDONG-201",
                        ThoiDiemKetThuc = now.AddDays(30),
                        NguoiThue = new NguoiThue { HoVaTen = "Nguyễn Văn A", Email = "a@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var stateStore = new FakeContractExpiryAlertStateStore();
            await stateStore.MarkAsSentAsync("201_30"); // Already marked
            var settings = new ContractAlertSettings { Enabled = true, AlertDays = new List<int> { 30, 15 }, AdminEmail = null };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            // Act
            var count = await useCase.ExecuteAsync();

            // Assert
            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task ContractExpiryAlert_FailedDelivery_ShouldNotMarkState()
        {
            var now = DateTime.UtcNow.Date;
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 301,
                        MaHopDong = "HDONG-301",
                        ThoiDiemKetThuc = now.AddDays(15),
                        NguoiThue = new NguoiThue { HoVaTen = "Trần Văn B", Email = "b@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService { ReturnSuccess = false };
            var stateStore = new FakeContractExpiryAlertStateStore();
            var settings = new ContractAlertSettings { Enabled = true, AlertDays = new List<int> { 30, 15 } };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.False(await stateStore.HasBeenSentAsync("301_15"));
        }

        [Fact]
        public async Task ContractExpiryAlert_DisabledSettings_ShouldReturnZero()
        {
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 401,
                        ThoiDiemKetThuc = DateTime.UtcNow.Date.AddDays(30),
                        NguoiThue = new NguoiThue { Email = "x@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var stateStore = new FakeContractExpiryAlertStateStore();
            var settings = new ContractAlertSettings { Enabled = false, AlertDays = new List<int> { 30 } };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task ContractExpiryAlert_WithAdminEmail_ShouldSendToAdminAndTenant()
        {
            var now = DateTime.UtcNow.Date;
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 501,
                        MaHopDong = "HDONG-501",
                        ThoiDiemKetThuc = now.AddDays(30),
                        NguoiThue = new NguoiThue { HoVaTen = "Lê Văn C", Email = "c@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var stateStore = new FakeContractExpiryAlertStateStore();
            var settings = new ContractAlertSettings { Enabled = true, AlertDays = new List<int> { 30 }, AdminEmail = "admin@test.com" };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(2, emailService.SentCount);
            Assert.True(await stateStore.HasBeenSentAsync("501_30"));
        }

        [Fact]
        public async Task ContractExpiryAlert_CancellationPropagation_ShouldStopProcessing()
        {
            var now = DateTime.UtcNow.Date;
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 601,
                        ThoiDiemKetThuc = now.AddDays(30),
                        NguoiThue = new NguoiThue { Email = "d@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var stateStore = new FakeContractExpiryAlertStateStore();
            var settings = new ContractAlertSettings { Enabled = true, AlertDays = new List<int> { 30 } };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var count = await useCase.ExecuteAsync(cts.Token);

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_DisabledSettings_ShouldReturnZero()
        {
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 201,
                        MaHoaDon = "HD-201",
                        HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "x@test.com" } }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore();
            var settings = new AutoReminderSettings { Enabled = false, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_MissingEmail_ShouldSkipInvoice()
        {
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 301,
                        MaHoaDon = "HD-301",
                        HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "" } }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore();
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_NullPdf_ShouldSkipInvoice()
        {
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 401,
                        MaHoaDon = "HD-401",
                        HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "test@test.com" } }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonServiceNullPdf();
            var stateStore = new FakeInvoiceReminderStateStore();
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_StateStoreThrowsOnMark_ShouldNotCountAsSent_AndNotPersistState()
        {
            var store = new FakeHoaDonStore
            {
                Invoices = new List<HoaDon>
                {
                    new()
                    {
                        HoaDonId = 501,
                        MaHoaDon = "HD-501",
                        HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "failstore@test.com" } }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var hoaDonService = new FakeHoaDonService();
            var stateStore = new FakeInvoiceReminderStateStore { ThrowOnMark = true };
            var settings = new AutoReminderSettings { Enabled = true, OverdueDays = 5 };
            var useCase = new InvoiceReminderUseCase(store, emailService, hoaDonService, settings, stateStore, NullLogger<InvoiceReminderUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            // Email sent, but store mark failed -> count should not increment, state should not be persisted
            Assert.Equal(0, count);
            Assert.Equal(1, emailService.SentCount);
            Assert.False(await stateStore.HasBeenSentAsync(501));
        }

        [Fact]
        public async Task ContractExpiryAlert_StateStoreThrowsOnMark_ShouldNotCountAsSent_AndNotPersistState()
        {
            var now = DateTime.UtcNow.Date;
            var store = new FakeHopDongStore
            {
                Contracts = new List<HopDong>
                {
                    new()
                    {
                        HopDongId = 701,
                        MaHopDong = "HDONG-701",
                        ThoiDiemKetThuc = now.AddDays(30),
                        NguoiThue = new NguoiThue { HoVaTen = "Nguyễn Văn Fail", Email = "failalert@test.com" }
                    }
                }
            };
            var emailService = new FakeEmailService();
            var stateStore = new FakeContractExpiryAlertStateStore { ThrowOnMark = true };
            var settings = new ContractAlertSettings { Enabled = true, AlertDays = new List<int> { 30 }, AdminEmail = null };
            var useCase = new ContractExpiryAlertUseCase(store, emailService, settings, stateStore, NullLogger<ContractExpiryAlertUseCase>.Instance);

            var count = await useCase.ExecuteAsync();

            // Email sent, but store mark failed -> count should not increment, state should not be persisted
            Assert.Equal(0, count);
            Assert.Equal(1, emailService.SentCount);
            Assert.False(await stateStore.HasBeenSentAsync("701_30"));
        }

        private class FakeHoaDonServiceNullPdf : IHoaDonService
        {
            public Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id) =>
                Task.FromResult<HoaDonChiTietRes?>(new HoaDonChiTietRes { HoaDonId = id, MaHoaDon = "HD-" + id });

            public Task<byte[]?> ExportPdfAsync(int id) => Task.FromResult<byte[]?>(null);

            public Task<PhatSinhHoaDonResult> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, List<int> selectedPhongTroIds) => throw new NotImplementedException();
            public Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> DeleteHoaDonAsync(int id) => throw new NotImplementedException();
            public Task<byte[]?> ExportExcelAsync(int hoaDonId) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId) => throw new NotImplementedException();
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId) => throw new NotImplementedException();
        }
    }
}
