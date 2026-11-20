using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class BackgroundJobUseCaseTests
    {
        private class FakeHoaDonStore : IHoaDonStore
        {
            public List<HoaDon> Invoices { get; set; } = new();
            public DateTime? LastThresholdUtc { get; private set; }

            public Task<IReadOnlyList<HoaDon>> GetOverdueInvoicesAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default)
            {
                LastThresholdUtc = thresholdUtc;
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
            public Task<DataTableResponse<HoaDonRes>> GetHoaDonsDataTableAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, IReadOnlyList<int>? allowedBranchIds = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HoaDonChiTietRes?> GetHoaDonDetailByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<HoaDon?> GetActiveHoaDonByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HoaDonRes>> GetUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<int>> GetContractIdsByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<HoaDonRes>> GetInvoicesByContractIdsAsync(IReadOnlyList<int> contractIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<int?> GetHoaDonBranchIdAsync(int hoaDonId, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
            public Task<IReadOnlyDictionary<int, int>> GetRoomBranchIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
            public Task<IReadOnlyList<HoaDon>> GetInvoicesByMeterReadingIdsAsync(IReadOnlyList<int> meterReadingIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HoaDon>>(new List<HoaDon>());
            public Task AddInvoicesAsync(IEnumerable<HoaDon> invoices, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public void UpdateSuCos(IEnumerable<YeuCauSuCo> suCos) => throw new NotImplementedException();
            public void RemoveChiTietHoaDons(IEnumerable<ChiTietHoaDon> chiTiets) => throw new NotImplementedException();
            public void UpdateHoaDon(HoaDon hoaDon) => throw new NotImplementedException();
            public Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class FakeHopDongStore : IHopDongStore
        {
            public List<HopDong> Contracts { get; set; } = new();
            public List<HopDong> Expired { get; set; } = new();
            public DateTime? ExpiredBeforeUtc { get; private set; }
            public int ExpiredCalls { get; private set; }
            public bool ThrowOnExpiredQuery { get; set; }

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
            public Task LockRoomForContractAsync(int phongTroId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
            public Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
            {
                ExpiredCalls++;
                ExpiredBeforeUtc = nowUtc;
                if (ThrowOnExpiredQuery)
                {
                    throw new InvalidOperationException("Mất kết nối DB");
                }
                return Task.FromResult<IReadOnlyList<HopDong>>(Expired);
            }
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
            public bool ThrowOnSend { get; set; }
            public int SentCount { get; private set; }
            public List<string> Recipients { get; } = new();

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                if (ReturnSuccess)
                {
                    SentCount++;
                    Recipients.Add(toEmail);
                    return Task.FromResult((true, string.Empty));
                }
                return Task.FromResult((false, "Email sending failed"));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
            {
                if (ThrowOnSend)
                {
                    throw new InvalidOperationException("Gửi email lỗi");
                }
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

                        public Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, int actorId) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req, int actorId = 0) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetEmployeeInvoiceListAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, int actorId) => throw new NotImplementedException();
            public Task<HoaDonChiTietRes?> GetEmployeeInvoiceDetailAsync(int id, int actorId) => throw new NotImplementedException();
            public Task<byte[]?> ExportExcelAsync(int hoaDonId) => throw new NotImplementedException();
            public Task<byte[]?> ExportEmployeeExcelAsync(int hoaDonId, int actorId) => throw new NotImplementedException();
            public Task<byte[]?> ExportEmployeePdfAsync(int hoaDonId, int actorId) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetEmployeeUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, int actorId) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId) => throw new NotImplementedException();
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> CheckInvoicePermissionAsync(int hoaDonId, int actorId, string actionCode) => throw new NotImplementedException();
        }

        // 5/11/2026 10:00 giờ VN. Hôm nay theo giờ VN là 5/11, bắt đầu lúc 4/11 17:00 UTC.
        private static readonly DateTimeOffset BayGio = new(2026, 11, 5, 3, 0, 0, TimeSpan.Zero);

        // Hóa đơn có hạn 23:59:59 giờ VN của ngày cách hôm nay soNgayQuaHan ngày.
        private static HoaDon OverdueInvoice(int id, int soNgayQuaHan, string? email = "tenant@test.com") => new()
        {
            HoaDonId = id,
            MaHoaDon = "HD-" + id,
            HanThanhToan = new DateTime(2026, 11, 5, 16, 59, 59, DateTimeKind.Utc).AddDays(-soNgayQuaHan),
            HopDong = new HopDong { NguoiThue = new NguoiThue { Email = email } }
        };

        private static InvoiceReminderUseCase NewInvoiceUseCase(FakeHoaDonStore store, FakeEmailService email, IHoaDonService? hoaDonService = null, bool enabled = true, int overdueDays = 5, int repeatEveryDays = 5)
            => new(store, email, hoaDonService ?? new FakeHoaDonService(),
                new AutoReminderSettings { Enabled = enabled, OverdueDays = overdueDays, RepeatEveryDays = repeatEveryDays },
                new FixedTimeProvider(BayGio), NullLogger<InvoiceReminderUseCase>.Instance);

        [Fact]
        public async Task InvoiceReminder_FirstMilestone_ShouldSendEmail()
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(101, 5) } };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService).ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(1, emailService.SentCount);
        }

        [Theory]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(15, true)]
        [InlineData(40, true)]
        [InlineData(4, false)]
        [InlineData(6, false)]
        [InlineData(9, false)]
        [InlineData(11, false)]
        public async Task InvoiceReminder_SendsOnlyOnMilestoneDays(int soNgayQuaHan, bool expectedSend)
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(102, soNgayQuaHan) } };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService).ExecuteAsync();

            Assert.Equal(expectedSend ? 1 : 0, count);
            Assert.Equal(expectedSend ? 1 : 0, emailService.SentCount);
        }

        [Theory]
        [InlineData(5, true)]
        [InlineData(10, false)]
        public async Task InvoiceReminder_RepeatZero_SendsOnlyFirstMilestone(int soNgayQuaHan, bool expectedSend)
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(103, soNgayQuaHan) } };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService, repeatEveryDays: 0).ExecuteAsync();

            Assert.Equal(expectedSend ? 1 : 0, count);
        }

        [Fact]
        public async Task InvoiceReminder_CountsOverdueDaysByVietnamDate()
        {
            // 31/10 23:59 giờ VN là quá hạn 5 ngày; 1/11 00:00 giờ VN (31/10 17:00 UTC) mới quá hạn 4 ngày.
            var store = new FakeHoaDonStore
            {
                Invoices =
                {
                    new HoaDon { HoaDonId = 104, MaHoaDon = "HD-104", HanThanhToan = new DateTime(2026, 10, 31, 16, 59, 0, DateTimeKind.Utc), HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "a@test.com" } } },
                    new HoaDon { HoaDonId = 105, MaHoaDon = "HD-105", HanThanhToan = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "b@test.com" } } }
                }
            };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService).ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(new[] { "a@test.com" }, emailService.Recipients);
        }

        [Fact]
        public async Task InvoiceReminder_WithoutDueDate_UsesSentDate()
        {
            var store = new FakeHoaDonStore
            {
                Invoices =
                {
                    new HoaDon { HoaDonId = 106, MaHoaDon = "HD-106", NgayGui = new DateTime(2026, 10, 26, 2, 0, 0, DateTimeKind.Utc), HopDong = new HopDong { NguoiThue = new NguoiThue { Email = "c@test.com" } } }
                }
            };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService).ExecuteAsync();

            Assert.Equal(1, count); // 26/10 giờ VN tới 5/11 là 10 ngày: mốc thứ hai.
        }

        [Fact]
        public async Task InvoiceReminder_QueriesFromStartOfVietnamDay()
        {
            var store = new FakeHoaDonStore();

            await NewInvoiceUseCase(store, new FakeEmailService()).ExecuteAsync();

            // Đầu ngày 6/11 giờ VN lùi 5 ngày: mọi hóa đơn quá hạn từ 5 ngày đều có hạn trước mốc này.
            Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), store.LastThresholdUtc);
        }

        [Fact]
        public async Task InvoiceReminder_FailedDelivery_ShouldNotCount()
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(107, 5) } };

            var count = await NewInvoiceUseCase(store, new FakeEmailService { ReturnSuccess = false }).ExecuteAsync();

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task InvoiceReminder_CancellationPropagation_ShouldStopProcessing()
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(108, 5) } };
            var emailService = new FakeEmailService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var count = await NewInvoiceUseCase(store, emailService).ExecuteAsync(cts.Token);

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_DisabledSettings_ShouldReturnZero()
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(109, 5) } };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService, enabled: false).ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
            Assert.Null(store.LastThresholdUtc);
        }

        [Fact]
        public async Task InvoiceReminder_MissingEmail_ShouldSkipInvoice()
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(110, 5, email: "") } };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService).ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task InvoiceReminder_NullPdf_ShouldSkipInvoice()
        {
            var store = new FakeHoaDonStore { Invoices = { OverdueInvoice(111, 5) } };
            var emailService = new FakeEmailService();

            var count = await NewInvoiceUseCase(store, emailService, new FakeHoaDonServiceNullPdf()).ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        private static HopDong ExpiringContract(int id, int daysLeft, string email = "a@test.com") => new()
        {
            HopDongId = id,
            MaHopDong = "HDONG-" + id,
            ThoiDiemKetThuc = BayGio.UtcDateTime.Date.AddDays(daysLeft),
            NguoiThue = new NguoiThue { HoVaTen = "Nguyễn Văn A", Email = email }
        };

        private static HopDong ExpiredContract(int id, DateTime endUtc) => new()
        {
            HopDongId = id,
            MaHopDong = "HDONG-" + id,
            PhongTroId = 9,
            ThoiDiemKetThuc = endUtc,
            PhongTro = new PhongTro { SoPhong = "P101", ChiNhanh = new ChiNhanh { TenChiNhanh = "CN Một" } },
            NguoiThue = new NguoiThue { HoVaTen = "Nguyễn Văn A" }
        };

        // 23:59:59 giờ VN ngày 5/11: hợp đồng hết hạn hôm nay.
        private static readonly DateTime HetHanHomNayUtc = new(2026, 11, 5, 16, 59, 59, DateTimeKind.Utc);

        private static ContractExpiryAlertUseCase NewExpiryUseCase(FakeHopDongStore store, FakeEmailService email, FakeThongBaoService thongBao, DateTimeOffset? now = null, bool enabled = true, string? adminEmail = null)
            => new(store, email, new ContractAlertSettings { Enabled = enabled, AlertDays = new List<int> { 30, 15 }, AdminEmail = adminEmail }, thongBao, new FixedTimeProvider(now ?? BayGio), NullLogger<ContractExpiryAlertUseCase>.Instance);

        [Fact]
        public async Task ContractExpiryAlert_OnAlertDay_ShouldSendAlert()
        {
            var store = new FakeHopDongStore { Contracts = { ExpiringContract(201, 30) } };
            var emailService = new FakeEmailService();

            var count = await NewExpiryUseCase(store, emailService, new FakeThongBaoService()).ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(1, emailService.SentCount);
        }

        [Theory]
        [InlineData(29)]
        [InlineData(16)]
        [InlineData(31)]
        public async Task ContractExpiryAlert_NotOnAlertDay_ShouldSkip(int daysLeft)
        {
            var store = new FakeHopDongStore { Contracts = { ExpiringContract(202, daysLeft) } };
            var emailService = new FakeEmailService();

            var count = await NewExpiryUseCase(store, emailService, new FakeThongBaoService()).ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task ContractExpiryAlert_FailedDelivery_ShouldNotCount()
        {
            var store = new FakeHopDongStore { Contracts = { ExpiringContract(301, 15) } };

            var count = await NewExpiryUseCase(store, new FakeEmailService { ReturnSuccess = false }, new FakeThongBaoService()).ExecuteAsync();

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ContractExpiryAlert_WithAdminEmail_ShouldSendToAdminAndTenant()
        {
            var store = new FakeHopDongStore { Contracts = { ExpiringContract(501, 30) } };
            var emailService = new FakeEmailService();

            var count = await NewExpiryUseCase(store, emailService, new FakeThongBaoService(), adminEmail: "admin@test.com").ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(2, emailService.SentCount);
        }

        [Fact]
        public async Task ContractExpiryAlert_CancellationPropagation_ShouldStopProcessing()
        {
            var store = new FakeHopDongStore { Contracts = { ExpiringContract(601, 30) } };
            var emailService = new FakeEmailService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var count = await NewExpiryUseCase(store, emailService, new FakeThongBaoService()).ExecuteAsync(cts.Token);

            Assert.Equal(0, count);
            Assert.Equal(0, emailService.SentCount);
        }

        [Fact]
        public async Task ExpiryAlert_SendsAppNotice_WhenEndDateIsToday()
        {
            var store = new FakeHopDongStore { Expired = { ExpiredContract(11, HetHanHomNayUtc) } };
            var thongBao = new FakeThongBaoService();

            var count = await NewExpiryUseCase(store, new FakeEmailService(), thongBao).ExecuteAsync();

            Assert.Equal(1, count);
            var sent = Assert.Single(thongBao.Sent);
            Assert.Equal("Phong", sent.Kind);
            Assert.Equal("/QuanLyNhaTro/HopDong/ChiTiet/11", sent.Link);
            Assert.Contains("05/11/2026", sent.NoiDung);
        }

        [Fact]
        public async Task ExpiryAlert_StartOfTodayVn_CountsAsToday()
        {
            // 00:00 giờ VN ngày 5/11 là hôm nay; 23:59:59 giờ VN ngày 4/11 là hôm qua.
            var store = new FakeHopDongStore
            {
                Expired =
                {
                    ExpiredContract(21, new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc)),
                    ExpiredContract(22, new DateTime(2026, 11, 4, 16, 59, 59, DateTimeKind.Utc))
                }
            };
            var thongBao = new FakeThongBaoService();

            await NewExpiryUseCase(store, new FakeEmailService(), thongBao).ExecuteAsync();

            var sent = Assert.Single(thongBao.Sent);
            Assert.Equal("/QuanLyNhaTro/HopDong/ChiTiet/21", sent.Link);
        }

        [Fact]
        public async Task ExpiryAlert_ContractExpiredOnEarlierDay_NoAppNotice()
        {
            // Hết hạn từ trước: không báo lại mỗi sáng, mục KH4 của Việc cần làm giữ lời nhắc.
            var store = new FakeHopDongStore { Expired = { ExpiredContract(12, new DateTime(2026, 10, 25, 16, 59, 59, DateTimeKind.Utc)) } };
            var thongBao = new FakeThongBaoService();

            var count = await NewExpiryUseCase(store, new FakeEmailService(), thongBao).ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Empty(thongBao.Sent);
        }

        [Fact]
        public async Task ExpiryAlert_PassesStartOfTomorrowVn()
        {
            var store = new FakeHopDongStore();
            var useCase = NewExpiryUseCase(store, new FakeEmailService(), new FakeThongBaoService(), new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero));

            await useCase.ExecuteAsync();

            Assert.Equal(new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc), store.ExpiredBeforeUtc);
        }

        [Fact]
        public async Task ExpiryAlert_AppFailure_ShouldNotCount()
        {
            var store = new FakeHopDongStore { Expired = { ExpiredContract(13, HetHanHomNayUtc) } };

            var count = await NewExpiryUseCase(store, new FakeEmailService(), new FakeThongBaoService { FailOnSend = true }).ExecuteAsync();

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ExpiryAlert_EmailFailure_DoesNotBlockApp()
        {
            var store = new FakeHopDongStore
            {
                Contracts = { ExpiringContract(14, 30) },
                Expired = { ExpiredContract(15, HetHanHomNayUtc) }
            };
            var thongBao = new FakeThongBaoService();

            await NewExpiryUseCase(store, new FakeEmailService { ThrowOnSend = true }, thongBao).ExecuteAsync();

            Assert.Single(thongBao.Sent);
        }

        [Fact]
        public async Task ExpiryAlert_AppFailure_DoesNotBlockEmail()
        {
            var store = new FakeHopDongStore
            {
                Contracts = { ExpiringContract(16, 30) },
                Expired = { ExpiredContract(17, HetHanHomNayUtc) }
            };
            var email = new FakeEmailService();

            var count = await NewExpiryUseCase(store, email, new FakeThongBaoService { ThrowOnSend = true }).ExecuteAsync();

            Assert.Equal(1, email.SentCount);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task ExpiryAlert_StoreFailure_ThrowsBeforeSendingAnything()
        {
            // Job thử lại cả lượt khi use case ném lỗi; lỗi đọc DB phải xảy ra trước khi gửi email để không gửi trùng.
            var store = new FakeHopDongStore
            {
                Contracts = { ExpiringContract(19, 30) },
                ThrowOnExpiredQuery = true
            };
            var email = new FakeEmailService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => NewExpiryUseCase(store, email, new FakeThongBaoService()).ExecuteAsync());

            Assert.Equal(0, email.SentCount);
        }

        [Fact]
        public async Task ExpiryAlert_Disabled_DoesNotQueryStore()
        {
            var store = new FakeHopDongStore { Expired = { ExpiredContract(18, HetHanHomNayUtc) } };

            var count = await NewExpiryUseCase(store, new FakeEmailService(), new FakeThongBaoService(), enabled: false).ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(0, store.ExpiredCalls);
        }

        private class FakeHoaDonServiceNullPdf : IHoaDonService
        {
            public Task<HoaDonChiTietRes?> GetHoaDonByIdAsync(int id) =>
                Task.FromResult<HoaDonChiTietRes?>(new HoaDonChiTietRes { HoaDonId = id, MaHoaDon = "HD-" + id });

            public Task<byte[]?> ExportPdfAsync(int id) => Task.FromResult<byte[]?>(null);

                        public Task<List<PhatSinhPreviewRes>> PreviewPhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam, int actorId) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> UpdateHoaDonAsync(int hoaDonId, UpdateHoaDonReq req, int actorId = 0) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai) => throw new NotImplementedException();
            public Task<DataTableResponse<HoaDonRes>> GetEmployeeInvoiceListAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, int actorId) => throw new NotImplementedException();
            public Task<HoaDonChiTietRes?> GetEmployeeInvoiceDetailAsync(int id, int actorId) => throw new NotImplementedException();
            public Task<byte[]?> ExportExcelAsync(int hoaDonId) => throw new NotImplementedException();
            public Task<byte[]?> ExportEmployeeExcelAsync(int hoaDonId, int actorId) => throw new NotImplementedException();
            public Task<byte[]?> ExportEmployeePdfAsync(int hoaDonId, int actorId) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetDanhSachHoaDonChuaThanhToanAsync(int chiNhanhId, int thang, int nam) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetEmployeeUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, int actorId) => throw new NotImplementedException();
            public Task<List<HoaDonRes>> GetHoaDonsByNguoiThueIdAsync(int nguoiThueId) => throw new NotImplementedException();
            public Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId) => throw new NotImplementedException();
            public Task<(bool IsSuccess, string? ErrorMessage)> CheckInvoicePermissionAsync(int hoaDonId, int actorId, string actionCode) => throw new NotImplementedException();
        }
    }
}
