using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Thông báo sau khi webhook SePay ghi sổ (spec thanh toán trực tuyến §5.3, §6): Telegram, email biên lai, thông báo trong
    // app, sự kiện SignalR. Telegram và email là fake dùng chung cả bộ test nên mọi so khớp lọc theo mã tham chiếu hoặc hóa đơn duy nhất.
    [Collection(WebTestCollection.Name)]
    public class SePayNotificationTests
    {
        private const string Url = "/api/webhooks/payment/sepay";
        private const string DefaultChat = "-100100";
        private const string BranchChat = "-100200";

        private readonly CustomWebApplicationFactory _factory;

        public SePayNotificationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seeded(
            int InvoiceId, string MaHoaDon, int BranchId, int StaffUserId, int OtherBranchStaffUserId, int TenantUserId, string TenantEmail);

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static byte[] Payload(int hoaDonId, string? reference, long amount = 2_000_000, string? content = null)
        {
            content ??= $"TTPTHD{hoaDonId}";
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Random.Shared.Next(1, int.MaxValue),
                gateway = "MBBank",
                transactionDate = "2026-10-03 09:30:00",
                accountNumber = CustomWebApplicationFactory.TestBankAccount,
                subAccount = (string?)null,
                code = (string?)null,
                content,
                transferType = "in",
                transferAmount = amount,
                accumulated = 0,
                referenceCode = reference,
                description = content
            });
        }

        private string Sign(string timestamp, byte[] body)
        {
            var data = Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.SePayOptionsInstance.WebhookSecret));
            return "sha256=" + Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
        }

        private HttpRequestMessage Signed(byte[] body)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", Sign(timestamp, body));
            return request;
        }

        private static async Task<(HttpStatusCode Status, string Body)> SendAsync(WebApplicationFactory<Program> factory, HttpRequestMessage request)
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private Task<(HttpStatusCode Status, string Body)> SendAsync(HttpRequestMessage request) => SendAsync(_factory, request);

        // Chi nhánh có nhân viên, chi nhánh khác có nhân viên khác, khách thuê có tài khoản và email, một hóa đơn DaGui.
        private async Task<Seeded> SeedAsync(decimal tongTien = 2_000_000m)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_TB_{s}", MaChiNhanh = TestCodes.MaChiNhanh("T"), DiaChi = "12 Duong Test", SoDienThoai = "0900000003", MoTa = "TB" };
            var otherBranch = new ChiNhanh { TenChiNhanh = $"CN_TB2_{s}", MaChiNhanh = TestCodes.MaChiNhanh("U"), DiaChi = "34 Duong Test", SoDienThoai = "0900000004", MoTa = "TB2" };
            db.ChiNhanhs.AddRange(branch, otherBranch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var email = $"tb_{s}@test.com";
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000003", CCCD = $"079{s[..6]}3", Email = email };
            var staff = new NguoiDung { TenDangNhap = $"nv_tb_{s}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            var otherStaff = new NguoiDung { TenDangNhap = $"nv_tb2_{s}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            db.PhongTros.Add(room);
            db.NguoiThues.Add(tenant);
            db.NguoiDungs.AddRange(staff, otherStaff);
            await db.SaveChangesAsync();

            var tenantUser = new NguoiDung { TenDangNhap = $"kh_tb_{s}", MatKhauHash = "hash", Role = Role.KhachThue, IsActive = true, NguoiThueId = tenant.NguoiThueId };
            db.NguoiDungs.Add(tenantUser);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staff.NguoiDungId, NgayPhanCong = DateTime.UtcNow });
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh { NguoiDungId = otherStaff.NguoiDungId, ChiNhanhId = otherBranch.ChiNhanhId, IsActive = true, NguoiPhanCongId = otherStaff.NguoiDungId, NgayPhanCong = DateTime.UtcNow });

            var contract = new HopDong
            {
                MaHopDong = $"HD_{s}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-2),
                TienCocPhong = 2000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.Add(contract);
            await db.SaveChangesAsync();

            var invoice = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-SPT-{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow.AddDays(-1),
                HanThanhToan = DateTime.UtcNow.AddDays(6),
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = tongTien, SoLuong = 1, TongTien = tongTien }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            return new Seeded(invoice.HoaDonId, invoice.MaHoaDon, branch.ChiNhanhId, staff.NguoiDungId, otherStaff.NguoiDungId, tenantUser.NguoiDungId, email);
        }

        private List<(string ChatId, string Text)> TelegramFor(string reference)
            => _factory.FakeTelegramInstance.Messages.Where(m => m.Text.Contains(reference)).ToList();

        private List<(string ToEmail, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.PaymentReceiptEmail Data)> ReceiptsFor(int hoaDonId)
            => _factory.FakeEmailServiceInstance.PaymentReceiptCalls.Where(c => c.Data.HoaDonId == hoaDonId).ToList();

        private async Task<List<LichSuThanhToan>> LedgerAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.LichSuThanhToans.AsNoTracking().Where(l => l.HoaDonId == hoaDonId).ToListAsync();
        }

        // ---------- Telegram và email biên lai ----------

        [Fact]
        public async Task FullPayment_SendsOneTelegramMessageToTheDefaultChat_AndOneReceiptEmailToTheTenant()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();

            var (status, body) = await SendAsync(Signed(Payload(seeded.InvoiceId, reference)));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"success\": true}", body);

            var message = Assert.Single(TelegramFor(reference));
            Assert.Equal(DefaultChat, message.ChatId);
            Assert.Contains("THU TIỀN TRỌ THÀNH CÔNG", message.Text);
            Assert.Contains($"Tiến độ {seeded.MaHoaDon}: 2.000.000/2.000.000 VNĐ", message.Text);
            Assert.Contains("Vừa nhận: +2.000.000 VNĐ", message.Text);
            Assert.Contains("Kỳ: Tháng 09/2026", message.Text);
            Assert.Contains("03/10/2026 09:30:00", message.Text);

            var email = Assert.Single(ReceiptsFor(seeded.InvoiceId));
            Assert.Equal(seeded.TenantEmail, email.ToEmail);
            Assert.Equal(1, email.Data.DotThu);
            Assert.True(email.Data.DaThuDu);
            Assert.Equal(0m, email.Data.ConNo);
            Assert.Equal(2_000_000m, email.Data.SoTienLanNay);
            Assert.Equal(reference, email.Data.MaThamChieu);
            Assert.Equal($"https://tro.test/KhachThue/HoaDon?hoaDonId={seeded.InvoiceId}", email.Data.LinkXemBienLai);
            Assert.Equal("Chuyển khoản 24/7 (VietQR, tự động)", email.Data.PhuongThuc);
        }

        [Fact]
        public async Task PartialPayment_SendsThePartialMessage_AndAnInstalmentReceipt()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();

            var (status, _) = await SendAsync(Signed(Payload(seeded.InvoiceId, reference, amount: 100_000)));

            Assert.Equal(HttpStatusCode.OK, status);
            var message = Assert.Single(TelegramFor(reference));
            Assert.Contains("THU TIỀN TRỌ (ĐÓNG MỘT PHẦN)", message.Text);
            Assert.Contains("Còn thiếu: 1.900.000 VNĐ", message.Text);
            var email = Assert.Single(ReceiptsFor(seeded.InvoiceId));
            Assert.False(email.Data.DaThuDu);
            Assert.Equal(1_900_000m, email.Data.ConNo);
            Assert.Equal(100_000m, email.Data.LuyKeDaThanhToan);
        }

        [Fact]
        public async Task Overpayment_SendsTheSurplusLine_AndTheReceiptCarriesTheSurplus()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();

            await SendAsync(Signed(Payload(seeded.InvoiceId, reference, amount: 2_300_000)));

            var message = Assert.Single(TelegramFor(reference));
            Assert.Contains("Vừa nhận: +2.300.000 VNĐ", message.Text);
            Assert.Contains("Khách đóng dư +300.000 VNĐ", message.Text);
            Assert.Equal(300_000m, Assert.Single(ReceiptsFor(seeded.InvoiceId)).Data.TienThua);
        }

        // TC05: SePay gửi lại hoặc replay cùng một giao dịch.
        [Fact]
        public async Task SamePayloadSentThreeTimes_RecordsOnce_AndNotifiesOnce()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();
            var body = Payload(seeded.InvoiceId, reference);

            for (var i = 0; i < 3; i++)
            {
                var (status, _) = await SendAsync(Signed(body));
                Assert.Equal(HttpStatusCode.OK, status);
            }

            Assert.Single(await LedgerAsync(seeded.InvoiceId));
            Assert.Single(TelegramFor(reference));
            Assert.Single(ReceiptsFor(seeded.InvoiceId));
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Single(await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
        }

        [Fact]
        public async Task TransferWithoutAnInvoiceCode_SentThreeTimes_RaisesOneAlert_ToTheDefaultChatOnly()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();
            var body = Payload(seeded.InvoiceId, reference, content: "chuyen tien tro thang 9");

            for (var i = 0; i < 3; i++)
            {
                var (status, responseBody) = await SendAsync(Signed(body));
                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Equal("{\"success\": true}", responseBody);
            }

            var message = Assert.Single(TelegramFor(reference));
            Assert.Equal(DefaultChat, message.ChatId);
            Assert.Contains("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", message.Text);
            Assert.Contains("Nội dung chuyển khoản không có mã TTPTHD", message.Text);
            Assert.Contains("chuyen tien tro thang 9", message.Text);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
            Assert.Empty(ReceiptsFor(seeded.InvoiceId));
        }

        [Fact]
        public async Task TransferToAnotherAccount_RaisesAnAlert()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();
            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Random.Shared.Next(1, int.MaxValue),
                gateway = "MBBank",
                transactionDate = "2026-10-03 09:30:00",
                accountNumber = "9999999999",
                code = (string?)null,
                content = $"TTPTHD{seeded.InvoiceId}",
                transferType = "in",
                transferAmount = 2_000_000,
                referenceCode = reference,
                description = $"TTPTHD{seeded.InvoiceId}"
            });

            var (status, _) = await SendAsync(Signed(body));

            Assert.Equal(HttpStatusCode.OK, status);
            var message = Assert.Single(TelegramFor(reference));
            Assert.Contains("Tiền vào tài khoản 9999999999 khác tài khoản nhận tiền của hệ thống", message.Text);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task AlreadyPaidInvoice_RaisesAnAlert_AndDoesNotEmailAgain()
        {
            var seeded = await SeedAsync();
            await SendAsync(Signed(Payload(seeded.InvoiceId, NewReference())));
            var second = NewReference();

            await SendAsync(Signed(Payload(seeded.InvoiceId, second, amount: 500_000)));

            var message = Assert.Single(TelegramFor(second));
            Assert.Contains("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", message.Text);
            Assert.Contains($"Khách chuyển thêm 500.000đ cho hóa đơn {seeded.MaHoaDon} đã trả đủ", message.Text);
            Assert.Single(ReceiptsFor(seeded.InvoiceId));
            Assert.Single(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task BranchChat_ReceivesTheSameMessageAsTheDefaultChat()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();
            _factory.TelegramOptionsInstance.BranchChatIds[seeded.BranchId] = BranchChat;
            try
            {
                await SendAsync(Signed(Payload(seeded.InvoiceId, reference)));
                var alertReference = NewReference();
                await SendAsync(Signed(Payload(seeded.InvoiceId, alertReference, amount: 1_000)));

                var messages = TelegramFor(reference);
                Assert.Equal(new[] { DefaultChat, BranchChat }, messages.Select(m => m.ChatId).OrderBy(c => c, StringComparer.Ordinal).ToArray());
                Assert.Single(messages.Select(m => m.Text).Distinct());
                // Cảnh báo của hóa đơn đã xác định được chi nhánh cũng tới chat chi nhánh.
                Assert.Equal(new[] { DefaultChat, BranchChat }, TelegramFor(alertReference).Select(m => m.ChatId).OrderBy(c => c, StringComparer.Ordinal).ToArray());
            }
            finally
            {
                _factory.TelegramOptionsInstance.BranchChatIds.Remove(seeded.BranchId);
            }
        }

        [Fact]
        public async Task TelegramOff_SendsNoMessage_ButTheReceiptEmailStillGoesOut()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();
            _factory.TelegramOptionsInstance.Enabled = false;
            try
            {
                var (status, _) = await SendAsync(Signed(Payload(seeded.InvoiceId, reference)));

                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Empty(TelegramFor(reference));
                Assert.Single(ReceiptsFor(seeded.InvoiceId));
            }
            finally
            {
                _factory.TelegramOptionsInstance.Enabled = true;
            }
        }

        [Fact]
        public async Task WithoutAPublicBaseUrl_TheReceiptEmailHasNoLink()
        {
            var seeded = await SeedAsync();
            _factory.OnlinePaymentOptionsInstance.PublicBaseUrl = string.Empty;
            try
            {
                await SendAsync(Signed(Payload(seeded.InvoiceId, NewReference())));

                Assert.Null(Assert.Single(ReceiptsFor(seeded.InvoiceId)).Data.LinkXemBienLai);
            }
            finally
            {
                _factory.OnlinePaymentOptionsInstance.PublicBaseUrl = "https://tro.test";
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WhenTheReceiptEmailFails_TheWebhookStillReturns200_AndTheLedgerRowStays(bool throws)
        {
            var seeded = await SeedAsync();
            var reference = NewReference();
            if (throws)
            {
                _factory.FakeEmailServiceInstance.ThrowForAddress[seeded.TenantEmail] = true;
            }
            else
            {
                _factory.FakeEmailServiceInstance.ResultByAddress[seeded.TenantEmail] = (false, "SMTP từ chối");
            }

            try
            {
                var (status, body) = await SendAsync(Signed(Payload(seeded.InvoiceId, reference)));

                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Equal("{\"success\": true}", body);
                Assert.Single(await LedgerAsync(seeded.InvoiceId));
                Assert.Single(TelegramFor(reference));
                Assert.Single(ReceiptsFor(seeded.InvoiceId));
                Assert.Contains(_factory.LogSink.Entries, e =>
                    e.Level == LogLevel.Warning && e.Message.Contains("email biên lai") && e.Message.Contains($"hóa đơn {seeded.InvoiceId},"));
                Assert.DoesNotContain(_factory.LogSink.Entries, e => e.Level >= LogLevel.Error && e.Message.Contains(seeded.TenantEmail));
            }
            finally
            {
                _factory.FakeEmailServiceInstance.ThrowForAddress.TryRemove(seeded.TenantEmail, out _);
                _factory.FakeEmailServiceInstance.ResultByAddress.TryRemove(seeded.TenantEmail, out _);
            }
        }

        // ---------- Thông báo trong app ----------

        [Fact]
        public async Task Recorded_CreatesInAppNotifications_ForBranchStaffAndTenant_NotForTheStaffOfAnotherBranch()
        {
            var seeded = await SeedAsync();

            await SendAsync(Signed(Payload(seeded.InvoiceId, NewReference())));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var staff = Assert.Single(await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.StaffUserId).ToListAsync());
            var tenant = Assert.Single(await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
            Assert.Empty(await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.OtherBranchStaffUserId).ToListAsync());

            Assert.StartsWith("Đã nhận chuyển khoản 2.000.000đ", staff.TieuDe);
            Assert.Equal(staff.TieuDe, tenant.TieuDe);
            Assert.Equal($"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={seeded.InvoiceId}", staff.LinkDieuHuong);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={seeded.InvoiceId}", tenant.LinkDieuHuong);
        }

        private sealed class RecordingNotifier : IThongBaoNotifier
        {
            public ConcurrentQueue<(int UserId, object Payload)> Sent { get; } = new();
            public ConcurrentQueue<(int UserId, InvoicePaymentUpdatedEvent Payload)> PaymentUpdates { get; } = new();

            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default)
            {
                Sent.Enqueue((nguoiDungId, payload));
                return Task.CompletedTask;
            }

            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task SendInvoicePaymentUpdatedAsync(int nguoiDungId, InvoicePaymentUpdatedEvent payload, CancellationToken cancellationToken = default)
            {
                PaymentUpdates.Enqueue((nguoiDungId, payload));
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Recorded_SendsThePanelEventToTheTenant_AndPushesTheInAppNotifications()
        {
            var seeded = await SeedAsync();
            var recorder = new RecordingNotifier();
            using var host = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IThongBaoNotifier>();
                s.AddSingleton<IThongBaoNotifier>(recorder);
            }));

            var (status, _) = await SendAsync(host, Signed(Payload(seeded.InvoiceId, NewReference(), amount: 500_000)));

            Assert.Equal(HttpStatusCode.OK, status);
            var update = Assert.Single(recorder.PaymentUpdates);
            Assert.Equal(seeded.TenantUserId, update.UserId);
            Assert.Equal(new InvoicePaymentUpdatedEvent(seeded.InvoiceId, 1_500_000m, "Thanh toán một phần"), update.Payload);
            Assert.Contains(recorder.Sent, s => s.UserId == seeded.StaffUserId);
            Assert.Contains(recorder.Sent, s => s.UserId == seeded.TenantUserId);
            Assert.DoesNotContain(recorder.Sent, s => s.UserId == seeded.OtherBranchStaffUserId);
        }

        [Fact]
        public async Task PaidInFull_PanelEventHasNothingLeft()
        {
            var seeded = await SeedAsync();
            var recorder = new RecordingNotifier();
            using var host = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IThongBaoNotifier>();
                s.AddSingleton<IThongBaoNotifier>(recorder);
            }));

            await SendAsync(host, Signed(Payload(seeded.InvoiceId, NewReference())));

            var update = Assert.Single(recorder.PaymentUpdates);
            Assert.Equal(new InvoicePaymentUpdatedEvent(seeded.InvoiceId, 0m, "Đã thanh toán"), update.Payload);
        }

        private sealed class BrokenTelegram : QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ITelegramNotifier
        {
            public int Calls;
            public bool ThrowInstead;

            public Task<(bool IsSuccess, string ErrorMessage)> SendMessageAsync(string chatId, string htmlText, CancellationToken ct = default)
            {
                Interlocked.Increment(ref Calls);
                if (ThrowInstead)
                {
                    throw new InvalidOperationException("Telegram down");
                }

                return Task.FromResult((false, "Telegram trả mã 500"));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WhenTelegramFails_TheWebhookStillReturns200_TheLedgerStays_AndTheEmailGoesOut(bool throws)
        {
            var seeded = await SeedAsync();
            var broken = new BrokenTelegram { ThrowInstead = throws };
            using var host = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ITelegramNotifier>();
                s.AddSingleton<QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments.ITelegramNotifier>(broken);
            }));

            var (status, body) = await SendAsync(host, Signed(Payload(seeded.InvoiceId, NewReference())));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"success\": true}", body);
            Assert.Single(await LedgerAsync(seeded.InvoiceId));
            Assert.Equal(1, broken.Calls);
            Assert.Single(ReceiptsFor(seeded.InvoiceId));
            Assert.Contains(_factory.LogSink.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains($"hóa đơn {seeded.InvoiceId},"));
            Assert.DoesNotContain(_factory.LogSink.Entries, e => e.Level >= LogLevel.Error && e.Message.Contains($"hóa đơn {seeded.InvoiceId},"));
        }

        [Fact]
        public async Task TheAlert_EscapesMarkupInTheBankContent_BeforeItReachesTelegram()
        {
            var seeded = await SeedAsync();
            var reference = NewReference();

            var (status, _) = await SendAsync(Signed(Payload(seeded.InvoiceId, reference, content: "<b>chuyen</b> & <script>x</script>")));

            Assert.Equal(HttpStatusCode.OK, status);
            var text = Assert.Single(TelegramFor(reference)).Text;
            Assert.Contains("Nội dung: &lt;b&gt;chuyen&lt;/b&gt; &amp; &lt;script&gt;x&lt;/script&gt;", text);
            Assert.DoesNotContain("<b>", text);
            Assert.DoesNotContain("<script>", text);
        }

        [Fact]
        public async Task ACancelledInvoice_RaisesAnAlert_WithoutReceiptEmailOrPanelEvent()
        {
            var seeded = await SeedAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var invoice = await db.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
                invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                invoice.IsDeleted = true;
                await db.SaveChangesAsync();
            }

            var reference = NewReference();
            var (status, _) = await SendAsync(Signed(Payload(seeded.InvoiceId, reference)));

            Assert.Equal(HttpStatusCode.OK, status);
            var message = Assert.Single(TelegramFor(reference));
            Assert.Contains("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", message.Text);
            Assert.Contains($"Hóa đơn {seeded.MaHoaDon} không nhận tiền được: Hóa đơn đã hủy", message.Text);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
            Assert.Empty(ReceiptsFor(seeded.InvoiceId));
            using var verifyScope = _factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await verifyDb.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
        }

        [Fact]
        public async Task TwoDifferentTransfersForTheSameInvoice_GiveTwoInstalmentReceipts_AndTwoTelegramMessages()
        {
            var seeded = await SeedAsync();
            var first = NewReference();
            var second = NewReference();

            await SendAsync(Signed(Payload(seeded.InvoiceId, first, amount: 700_000)));
            await SendAsync(Signed(Payload(seeded.InvoiceId, second, amount: 1_300_000)));

            var receipts = ReceiptsFor(seeded.InvoiceId).OrderBy(r => r.Data.DotThu).ToList();
            Assert.Equal(2, receipts.Count);
            Assert.Equal((1, 700_000m, false, 1_300_000m), (receipts[0].Data.DotThu, receipts[0].Data.LuyKeDaThanhToan, receipts[0].Data.DaThuDu, receipts[0].Data.ConNo));
            Assert.Equal((2, 2_000_000m, true, 0m), (receipts[1].Data.DotThu, receipts[1].Data.LuyKeDaThanhToan, receipts[1].Data.DaThuDu, receipts[1].Data.ConNo));
            Assert.Contains("ĐÓNG MỘT PHẦN", Assert.Single(TelegramFor(first)).Text);
            Assert.Contains("THU TIỀN TRỌ THÀNH CÔNG", Assert.Single(TelegramFor(second)).Text);
        }

        [Fact]
        public async Task NoPanelEvent_ForAnAlertOrADuplicate()
        {
            var seeded = await SeedAsync();
            var recorder = new RecordingNotifier();
            using var host = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IThongBaoNotifier>();
                s.AddSingleton<IThongBaoNotifier>(recorder);
            }));
            var body = Payload(seeded.InvoiceId, NewReference());
            await SendAsync(host, Signed(body));

            await SendAsync(host, Signed(body));
            await SendAsync(host, Signed(Payload(seeded.InvoiceId, NewReference(), content: "khong co ma")));

            Assert.Single(recorder.PaymentUpdates);
        }
    }
}
