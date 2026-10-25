using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

// Màn đối soát giữ chỗ của nhân viên: chọn sẵn minh chứng chờ, tiền dạng 1.500.000 ₫,
// và báo rõ khi thời điểm thực nhận sớm hơn lúc duyệt.
[Collection(WebTestCollection.Name)]
public sealed class ReservationReconciliationScreenTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task DetailPreselectsPendingEvidenceAndExplainsEarlyReceivedTime()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminName = "ad_doisoat_" + suffix;
        int? branchId = null, roomId = null, guestUserId = null, holdId = null;
        try
        {
            int paymentRequestId, evidenceId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                Assert.True((await users.AddAsync(new CreateNguoiDungReq
                {
                    TenDangNhap = adminName, MatKhau = BranchScopeKit.Password,
                    Role = AppRole.Admin
                })).Success);
                var adminId = await db.NguoiDungs.Where(u => u.TenDangNhap == adminName)
                    .Select(u => u.NguoiDungId).SingleAsync();
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "D" + suffix[..7], TenChiNhanh = "Test " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                var guest = new NguoiDung
                {
                    TenDangNhap = "guest_doisoat_" + suffix, MatKhauHash = "test",
                    Role = Role.KhachVangLai
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.Add(guest);
                db.KhachVangLais.Add(new KhachVangLai
                {
                    NguoiDung = guest, HoTen = "Khách đối soát",
                    SoDienThoai = "0912345678", Email = "guest_doisoat_" + suffix + "@example.com"
                });
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "D" + suffix,
                    GiaThue = 2000000m, DienTich = 20, MoTa = "Test",
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                roomId = room.PhongTroId;
                guestUserId = guest.NguoiDungId;

                var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
                var requested = await reservations.RequestAsync(guest.NguoiDungId, room.PhongTroId, null);
                Assert.True(requested.Success, requested.Message);
                holdId = requested.Data;
                var now = DateTime.UtcNow;
                Assert.True((await reservations.ApproveAsync(adminId, holdId.Value,
                    1500000m, now.AddDays(1), now.AddDays(2))).Success);
                paymentRequestId = await db.YeuCauThanhToanGiuChos
                    .Where(p => p.YeuCauGiuChoId == holdId.Value)
                    .Select(p => p.YeuCauThanhToanGiuChoId).SingleAsync();
                var payments = scope.ServiceProvider.GetRequiredService<IReservationPaymentStore>();
                evidenceId = (await payments.AddEvidenceAsync(guest.NguoiDungId,
                    new EvidenceRecord(paymentRequestId,
                        "https://res.cloudinary.com/demo/hold-payment-proofs/" + suffix + ".jpg",
                        "hold-payment-proofs/" + suffix, 1500000m, now, "BANK-" + suffix, null),
                    now)).GetValueOrDefault();
                Assert.True(evidenceId > 0);
            }

            var detailPath = $"/QuanLyNhaTro/GiuCho/ChiTiet/{holdId}";
            var (client, token) = await BranchScopeKit.LoginAsync(factory, adminName, detailPath);
            var detailHtml = await client.GetStringAsync(detailPath);
            Assert.Matches($"<option value=\"{evidenceId}\" selected=\"selected\">", detailHtml);
            var decodedDetail = WebUtility.HtmlDecode(detailHtml);
            Assert.Contains("Số tiền giữ chỗ được duyệt: 1.500.000 ₫", decodedDetail);
            Assert.DoesNotContain("1,500,000", decodedDetail);
            Assert.Contains("1.500.000 ₫",
                WebUtility.HtmlDecode(await client.GetStringAsync("/QuanLyNhaTro/GiuCho")));

            var earlyLocal = DateTimeOffset.UtcNow.AddHours(-2)
                .ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-ddTHH:mm");
            var confirm = await client.PostAsync("/QuanLyNhaTro/GiuCho/XacNhanTien",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["reservationId"] = holdId.ToString()!,
                    ["paymentRequestId"] = paymentRequestId.ToString(),
                    ["evidenceId"] = evidenceId.ToString(),
                    ["bankReference"] = "BANK-" + suffix,
                    ["actualAmount"] = "1500000",
                    ["receivedAtLocal"] = earlyLocal
                }));
            Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
            var afterHtml = WebUtility.HtmlDecode(await client.GetStringAsync(detailPath));
            Assert.Contains("Thời điểm thực nhận sớm hơn lúc duyệt giữ chỗ", afterHtml);
            Assert.Contains("Tiền đã đối soát: 0 ₫", afterHtml);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (holdId.HasValue)
            {
                var paymentIds = await db.YeuCauThanhToanGiuChos
                    .Where(p => p.YeuCauGiuChoId == holdId.Value)
                    .Select(p => p.YeuCauThanhToanGiuChoId).ToArrayAsync();
                await db.GiaoDichGiuChos.Where(g =>
                    paymentIds.Contains(g.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.MinhChungThanhToanGiuChos.Where(e =>
                    paymentIds.Contains(e.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(h =>
                    paymentIds.Contains(h.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.YeuCauThanhToanGiuChos.Where(p =>
                    paymentIds.Contains(p.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
                await db.LichSuTrangThaiYeuCauGiuChos.Where(h =>
                    h.YeuCauGiuChoId == holdId.Value).ExecuteDeleteAsync();
                await db.YeuCauGiuChos.Where(h =>
                    h.YeuCauGiuChoId == holdId.Value).ExecuteDeleteAsync();
            }
            if (guestUserId.HasValue)
            {
                await db.KhachVangLais.Where(g => g.NguoiDungId == guestUserId.Value)
                    .ExecuteDeleteAsync();
                await db.NguoiDungs.Where(u => u.NguoiDungId == guestUserId.Value)
                    .ExecuteDeleteAsync();
            }
            if (roomId.HasValue)
                await db.PhongTros.Where(r => r.PhongTroId == roomId.Value).ExecuteDeleteAsync();
            if (branchId.HasValue)
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId.Value).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => u.TenDangNhap == adminName).ExecuteDeleteAsync();
        }
    }
}
