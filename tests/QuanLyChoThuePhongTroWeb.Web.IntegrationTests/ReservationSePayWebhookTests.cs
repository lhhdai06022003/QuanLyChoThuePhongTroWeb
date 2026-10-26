using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Webhook SePay ghi tiền giữ chỗ theo mã TTPTGC{id} (spec giữ chỗ §6.1, §10): cờ bật ghi một dòng do hệ thống,
    // gửi lại không ghi thêm, cờ tắt không ghi. Cờ được bật bằng WithWebHostBuilder, không đụng OnlinePaymentOptionsInstance dùng chung.
    [Collection(WebTestCollection.Name)]
    public class ReservationSePayWebhookTests
    {
        private const string Url = "/api/webhooks/payment/sepay";
        private const decimal HoldAmount = 500_000m;

        private readonly CustomWebApplicationFactory _factory;

        public ReservationSePayWebhookTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seeded(int HoldId, int PaymentId, int RoomId, int BranchId, int GuestUserId, int StaffUserId);

        private WebApplicationFactory<Program> Host(bool reservationEnabled)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<OnlinePaymentOptions>();
                s.AddSingleton(new OnlinePaymentOptions { PublicBaseUrl = "https://tro.test", ReservationEnabled = reservationEnabled });
            }));

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static byte[] Payload(int holdId, string reference, long amount = (long)HoldAmount)
        {
            var content = $"Nguyen Van A TTPTGC{holdId}";
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Random.Shared.Next(1, int.MaxValue),
                gateway = "MBBank",
                transactionDate = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"),
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

        private HttpRequestMessage Signed(byte[] body)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var data = Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.SePayOptionsInstance.WebhookSecret));
            var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", "sha256=" + Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant());
            return request;
        }

        private async Task<Seeded> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_GC_{s}", MaChiNhanh = TestCodes.MaChiNhanh("G"), DiaChi = "A", SoDienThoai = "0900000009", MoTa = "GC" };
            var guest = new NguoiDung { TenDangNhap = $"gc_guest_{s}", MatKhauHash = "hash", Role = Role.KhachVangLai, IsActive = true };
            var staff = new NguoiDung { TenDangNhap = $"gc_staff_{s}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            db.ChiNhanhs.Add(branch);
            db.NguoiDungs.AddRange(guest, staff);
            await db.SaveChangesAsync();

            var profile = new KhachVangLai { NguoiDungId = guest.NguoiDungId, HoTen = $"Khach GC {s}", SoDienThoai = "0911000009", Email = $"gc_{s}@test.com" };
            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"G_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "G", TrangThai = TrangThaiPhong.Trong };
            db.KhachVangLais.Add(profile);
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var deadline = DateTime.UtcNow.AddDays(1);
            var hold = new YeuCauGiuCho
            {
                KhachVangLaiId = profile.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                SoTienGiuCho = HoldAmount,
                HanThanhToan = deadline,
                HanKyHopDong = DateTime.UtcNow.AddDays(3),
                NguoiDuyetId = staff.NguoiDungId,
                NgayDuyet = DateTime.UtcNow,
                TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan
            };
            db.YeuCauGiuChos.Add(hold);
            await db.SaveChangesAsync();

            var payment = new YeuCauThanhToanGiuCho
            {
                YeuCauGiuChoId = hold.YeuCauGiuChoId,
                MaYeuCau = $"TTPTGC{hold.YeuCauGiuChoId}",
                NoiDungChuyenKhoan = $"TTPTGC{hold.YeuCauGiuChoId}",
                SoTien = HoldAmount,
                HanThanhToan = deadline,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = staff.NguoiDungId
            };
            db.YeuCauThanhToanGiuChos.Add(payment);
            await db.SaveChangesAsync();
            return new Seeded(hold.YeuCauGiuChoId, payment.YeuCauThanhToanGiuChoId, room.PhongTroId, branch.ChiNhanhId, guest.NguoiDungId, staff.NguoiDungId);
        }

        private async Task<(System.Collections.Generic.List<GiaoDichGiuCho> Rows, TrangThaiYeuCauGiuCho Hold)> StateAsync(Seeded seed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.GiaoDichGiuChos.AsNoTracking().Where(g => g.YeuCauThanhToanGiuChoId == seed.PaymentId).ToListAsync();
            var hold = await db.YeuCauGiuChos.AsNoTracking().SingleAsync(h => h.YeuCauGiuChoId == seed.HoldId);
            return (rows, hold.TrangThai);
        }

        [Fact]
        public async Task FlagOn_RecordsOneSystemRow_AndAResendWritesNoMore()
        {
            var seed = await SeedAsync();
            var reference = NewReference();
            var body = Payload(seed.HoldId, reference);
            var host = Host(reservationEnabled: true);

            var first = await Post(host, body);
            var second = await Post(host, body);

            Assert.Equal(HttpStatusCode.OK, first);
            Assert.Equal(HttpStatusCode.OK, second);
            var (rows, hold) = await StateAsync(seed);
            var row = Assert.Single(rows);
            Assert.Equal("SEPAY-" + reference, row.MaGiaoDich);
            Assert.Null(row.NguoiXacNhanId);
            Assert.Equal(HoldAmount, row.SoTienThucNhan);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, hold);

            // Nhân viên phụ trách phòng được báo trong app (Admin luôn có; nhân viên chưa phân công chi nhánh nên không bắt buộc).
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Contains(await db.ThongBaos.AsNoTracking().Where(t => t.LinkDieuHuong == $"/QuanLyNhaTro/GiuCho/ChiTiet/{seed.HoldId}").ToListAsync(),
                t => t.LinhVuc == "ThanhToan");
        }

        [Fact]
        public async Task FlagOff_ReturnsOk_AndWritesNothing()
        {
            var seed = await SeedAsync();
            var host = Host(reservationEnabled: false);

            var status = await Post(host, Payload(seed.HoldId, NewReference()));

            Assert.Equal(HttpStatusCode.OK, status);
            var (rows, hold) = await StateAsync(seed);
            Assert.Empty(rows);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, hold);
        }

        [Fact]
        public async Task FlagOn_UnknownReservationCode_ReturnsOk_AndWritesNothing()
        {
            var seed = await SeedAsync();
            var host = Host(reservationEnabled: true);

            var status = await Post(host, Payload(seed.HoldId + 900_000, NewReference()));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Empty((await StateAsync(seed)).Rows);
        }

        private async Task<HttpStatusCode> Post(WebApplicationFactory<Program> host, byte[] body)
        {
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.SendAsync(Signed(body));
            return response.StatusCode;
        }
    }
}
