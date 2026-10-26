using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // IPN VNPay ghi tiền giữ chỗ (spec giữ chỗ §6.2, §10): vnp_TxnRef GC{id}_{ticks} đi nhánh giữ chỗ, HD{id}_{ticks} vẫn đi nhánh hóa đơn.
    // VnPayOptions và cờ giữ chỗ bật bằng WithWebHostBuilder, không đụng instance dùng chung của fixture.
    [Collection(WebTestCollection.Name)]
    public class ReservationVnPayIpnTests
    {
        private const string Url = "/api/webhooks/payment/vnpay-ipn";
        private const string TmnCode = "TESTTMN1";
        private const string HashSecret = "test-vnpay-hash-secret";
        private const decimal HoldAmount = 500_000m;

        private readonly CustomWebApplicationFactory _factory;

        public ReservationVnPayIpnTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private WebApplicationFactory<Program> Host(bool reservationEnabled)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<VnPayOptions>();
                s.AddSingleton(new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = HashSecret });
                s.RemoveAll<OnlinePaymentOptions>();
                s.AddSingleton(new OnlinePaymentOptions { PublicBaseUrl = "https://tro.test", ReservationEnabled = reservationEnabled });
            }));

        private static string NewTransactionNo() => "VNG" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static string ToQueryString(IDictionary<string, string> query)
            => "?" + string.Join("&", query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        private static Dictionary<string, string> SignedQuery(int holdId, string transactionNo, Action<Dictionary<string, string>>? tweak = null)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = ((long)(HoldAmount * 100)).ToString(),
                ["vnp_BankCode"] = "NCB",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan giu cho TTPTGC" + holdId,
                ["vnp_PayDate"] = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"),
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = transactionNo,
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = VnPayProtocol.BuildReservationTxnRef(holdId, DateTime.UtcNow)
            };
            tweak?.Invoke(query);
            query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), HashSecret);
            return query;
        }

        private static async Task<string> RspCodeAsync(WebApplicationFactory<Program> host, IDictionary<string, string> query)
        {
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.GetAsync(Url + ToQueryString(query));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("RspCode").GetString()!;
        }

        private sealed record Seeded(int HoldId, int PaymentId);

        private async Task<Seeded> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_GV_{s}", MaChiNhanh = TestCodes.MaChiNhanh("V"), DiaChi = "A", SoDienThoai = "0900000008", MoTa = "GV" };
            var guest = new NguoiDung { TenDangNhap = $"gv_guest_{s}", MatKhauHash = "hash", Role = Role.KhachVangLai, IsActive = true };
            var staff = new NguoiDung { TenDangNhap = $"gv_staff_{s}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            db.ChiNhanhs.Add(branch);
            db.NguoiDungs.AddRange(guest, staff);
            await db.SaveChangesAsync();

            var profile = new KhachVangLai { NguoiDungId = guest.NguoiDungId, HoTen = $"Khach GV {s}", SoDienThoai = "0911000008", Email = $"gv_{s}@test.com" };
            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"V_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "V", TrangThai = TrangThaiPhong.Trong };
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
            return new Seeded(hold.YeuCauGiuChoId, payment.YeuCauThanhToanGiuChoId);
        }

        private async Task<List<GiaoDichGiuCho>> RowsAsync(Seeded seed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.GiaoDichGiuChos.AsNoTracking().Where(g => g.YeuCauThanhToanGiuChoId == seed.PaymentId).ToListAsync();
        }

        [Fact]
        public async Task WrongSignature_Returns97_AndWritesNothing()
        {
            var seed = await SeedAsync();
            var query = SignedQuery(seed.HoldId, NewTransactionNo());
            query["vnp_Amount"] = "100";

            Assert.Equal("97", await RspCodeAsync(Host(true), query));
            Assert.Empty(await RowsAsync(seed));
        }

        [Fact]
        public async Task ValidIpn_Returns00_AndRecordsOneSystemRow_ThenAResendReturns02()
        {
            var seed = await SeedAsync();
            var transactionNo = NewTransactionNo();
            var host = Host(true);
            var query = SignedQuery(seed.HoldId, transactionNo);

            Assert.Equal("00", await RspCodeAsync(host, query));
            Assert.Equal("02", await RspCodeAsync(host, query));

            var row = Assert.Single(await RowsAsync(seed));
            Assert.Equal("VNPAY-" + transactionNo, row.MaGiaoDich);
            Assert.Null(row.NguoiXacNhanId);
            Assert.Equal(HoldAmount, row.SoTienThucNhan);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho,
                (await db.YeuCauGiuChos.AsNoTracking().SingleAsync(h => h.YeuCauGiuChoId == seed.HoldId)).TrangThai);
        }

        [Fact]
        public async Task UnknownReservation_Returns01_AndWritesNothing()
        {
            var seed = await SeedAsync();

            Assert.Equal("01", await RspCodeAsync(Host(true), SignedQuery(seed.HoldId + 900_000, NewTransactionNo())));
            Assert.Empty(await RowsAsync(seed));
        }

        [Fact]
        public async Task FlagOff_Returns01_AndWritesNothing()
        {
            var seed = await SeedAsync();

            Assert.Equal("01", await RspCodeAsync(Host(false), SignedQuery(seed.HoldId, NewTransactionNo())));
            Assert.Empty(await RowsAsync(seed));
        }

        [Fact]
        public async Task FailedPayment_Returns00_AndWritesNothing()
        {
            var seed = await SeedAsync();

            Assert.Equal("00", await RspCodeAsync(Host(true), SignedQuery(seed.HoldId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "24")));
            Assert.Empty(await RowsAsync(seed));
        }
    }
}
