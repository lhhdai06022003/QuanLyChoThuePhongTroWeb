using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Thanh lý, gia hạn hợp đồng và biên bản thanh lý ở cổng quản lý: antiforgery, phân quyền chi nhánh, tiền cấn trừ, giờ VN.
    [Collection(WebTestCollection.Name)]
    public class ContractSettlementRenewalTests
    {
        private static readonly DateTime EndOct = new(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc);
        private const string Page = "/QuanLyNhaTro/QuanLyHopDong";

        private readonly CustomWebApplicationFactory _factory;

        public ContractSettlementRenewalTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Hợp đồng A: kết thúc 31/10/2026 23:59:59 VN, cọc 3 triệu; BranchScopeKit đã phân công nhân viên cho chi nhánh A.
        private async Task<BranchScopeWorld> PrepareAsync(decimal coc = 3_000_000m)
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contract = await db.HopDongs.FirstAsync(h => h.HopDongId == world.ContractA);
            contract.ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            contract.ThoiDiemKetThuc = EndOct;
            contract.TienCocPhong = coc;
            contract.TienThuePhong = 3_000_000m;
            await db.SaveChangesAsync();
            return world;
        }

        private async Task<int> AddInvoiceAsync(int hopDongId, int thang, int nam, decimal tong, TrangThaiPhatHanhHoaDon phatHanh = TrangThaiPhatHanhHoaDon.DaGui)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new HoaDon
            {
                HopDongId = hopDongId,
                MaHoaDon = $"HDT-{Guid.NewGuid():N}"[..20],
                Thang = thang,
                Nam = nam,
                TongTien = tong,
                TrangThaiPhatHanh = phatHanh,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = phatHanh == TrangThaiPhatHanhHoaDon.DaGui ? DateTime.UtcNow : null
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            return invoice.HoaDonId;
        }

        private async Task AddLedgerAsync(int hoaDonId, decimal soTien, PhuongThucThanhToan phuongThuc = PhuongThucThanhToan.TienMat)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = $"GD-{Guid.NewGuid():N}"[..24],
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = DateTime.UtcNow,
                NgayXacNhan = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private static Task<HttpResponseMessage> Settle(HttpClient client, string token, int id, decimal tongNo, bool xacNhan = false)
            => BranchScopeKit.SendJsonAsync(client, token, HttpMethod.Post, $"/HopDong/ThanhLy/{id}", new { tongNoLucMo = tongNo, xacNhanConNo = xacNhan });

        private static Task<HttpResponseMessage> Renew(HttpClient client, string token, int id, int thang = 11, int nam = 2026, string end = "2027-10-31")
            => BranchScopeKit.SendJsonAsync(client, token, HttpMethod.Post, $"/HopDong/GiaHan/{id}",
                new { thangBatDau = thang, namBatDau = nam, ngayKetThucMoi = end, tienThue = 3_500_000m, tienCoc = 3_000_000m, chepDieuKhoanCu = true });

        private static async Task<string> HtmlTextAsync(HttpResponseMessage res) => WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

        [Fact]
        public async Task ThanhLy_WithoutAntiforgery_Is400()
        {
            var world = await PrepareAsync();
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            var res = await Settle(client, string.Empty, world.ContractA, 0m);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Theory]
        [InlineData("/HopDong/ThanhLy/{0}")]
        [InlineData("/HopDong/GiaHan/{0}")]
        public async Task NullJsonBody_Is400_AndNothingChanges(string urlFormat)
        {
            var world = await PrepareAsync();
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            var req = new HttpRequestMessage(HttpMethod.Post, string.Format(urlFormat, world.ContractA))
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.Add("RequestVerificationToken", token);
            var res = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var status = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.Where(h => h.HopDongId == world.ContractA).Select(h => h.TrangThaiHopDong).FirstAsync());
            Assert.Equal(TrangThaiHopDong.DangHoatDong, status);
        }

        [Fact]
        public async Task ThanhLy_StaffOtherBranch_Is403_AndNothingChanges()
        {
            var world = await PrepareAsync();
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/QuanLyNhaTro/HopDong/ThanhLy/{world.ContractB}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Settle(client, token, world.ContractB, 0m)).StatusCode);

            var status = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.Where(h => h.HopDongId == world.ContractB).Select(h => h.TrangThaiHopDong).FirstAsync());
            Assert.Equal(TrangThaiHopDong.DangHoatDong, status);
        }

        [Fact]
        public async Task ThanhLy_StaffOwnBranch_Succeeds()
        {
            var world = await PrepareAsync();
            var invoiceId = await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/QuanLyNhaTro/HopDong/ThanhLy/{world.ContractA}")).StatusCode);
            var res = await Settle(client, token, world.ContractA, 2_000_000m);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var contract = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.FirstAsync(h => h.HopDongId == world.ContractA));
            var room = await BranchScopeKit.ReadAsync(_factory, db => db.PhongTros.FirstAsync(p => p.PhongTroId == world.RoomA));
            var ledger = await BranchScopeKit.ReadAsync(_factory, db => db.LichSuThanhToans.Where(l => l.HoaDonId == invoiceId).ToListAsync());
            var invoice = await BranchScopeKit.ReadAsync(_factory, db => db.HoaDons.FirstAsync(h => h.HoaDonId == invoiceId));
            Assert.Equal(TrangThaiHopDong.DaKetThuc, contract.TrangThaiHopDong);
            Assert.Equal(TrangThaiPhong.Trong, room.TrangThai);
            var offset = Assert.Single(ledger);
            Assert.Equal(PhuongThucThanhToan.CanTruTienCoc, offset.PhuongThucThanhToan);
            Assert.Equal(2_000_000m, offset.SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, invoice.TrangThaiHoaDon);
        }

        [Fact]
        public async Task ThanhLy_TwiceClick_SecondIs400()
        {
            var world = await PrepareAsync(coc: 5_000_000m);
            await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.OK, (await Settle(client, token, world.ContractA, 2_000_000m)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Settle(client, token, world.ContractA, 2_000_000m)).StatusCode);
        }

        [Fact]
        public async Task ThanhLy_DebtRemains_WithoutConfirm_Is400_WithConfirm_Is200()
        {
            var world = await PrepareAsync(coc: 1_000_000m);
            await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await Settle(client, token, world.ContractA, 2_000_000m, xacNhan: false)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Settle(client, token, world.ContractA, 2_000_000m, xacNhan: true)).StatusCode);
        }

        [Fact]
        public async Task ThanhLy_DebtChangedSinceOpen_Is400()
        {
            var world = await PrepareAsync();
            var invoiceId = await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);
            await AddLedgerAsync(invoiceId, 500_000m);

            var res = await Settle(client, token, world.ContractA, 2_000_000m);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("đã thay đổi", (await BranchScopeKit.JsonAsync(res)).GetProperty("message").GetString());
        }

        [Fact]
        public async Task ThanhLy_MissingPublishedReturnMonthInvoice_Is400()
        {
            var world = await PrepareAsync();
            await AddInvoiceAsync(world.ContractA, 9, 2026, 1_000_000m);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await Settle(client, token, world.ContractA, 1_000_000m, xacNhan: true)).StatusCode);
        }

        [Fact]
        public async Task ThanhLy_DraftInvoiceExists_Is400()
        {
            var world = await PrepareAsync();
            await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            await AddInvoiceAsync(world.ContractA, 11, 2026, 2_000_000m, TrangThaiPhatHanhHoaDon.Nhap);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await Settle(client, token, world.ContractA, 2_000_000m)).StatusCode);
        }

        [Fact]
        public async Task BienBan_FirstPrintPost_And_Reprint_ReturnPdf_AndUnsettledIs400()
        {
            var world = await PrepareAsync(coc: 5_000_000m);
            await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            var before = await BranchScopeKit.PostFormAsync(client, token, $"/HopDong/BienBanThanhLy/{world.ContractA}", new Dictionary<string, string> { ["ghiChu"] = "x" });
            Assert.Equal(HttpStatusCode.BadRequest, before.StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await Settle(client, token, world.ContractA, 2_000_000m)).StatusCode);

            var first = await BranchScopeKit.PostFormAsync(client, token, $"/HopDong/BienBanThanhLy/{world.ContractA}", new Dictionary<string, string> { ["ghiChu"] = "Hẹn trả trước 30/11" });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal("application/pdf", first.Content.Headers.ContentType?.MediaType);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString((await first.Content.ReadAsByteArrayAsync()).Take(4).ToArray()));

            var reprint = await client.GetAsync($"/HopDong/BienBanThanhLy/{world.ContractA}");
            Assert.Equal(HttpStatusCode.OK, reprint.StatusCode);
            Assert.Equal("application/pdf", reprint.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task GiaHan_Succeeds_CreatesGh1_AndOldEnded_AndDetailShowsChain()
        {
            var world = await PrepareAsync();
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);
            var oldCode = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.Where(h => h.HopDongId == world.ContractA).Select(h => h.MaHopDong).FirstAsync());

            var res = await Renew(client, token, world.ContractA);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var newCode = (await BranchScopeKit.JsonAsync(res)).GetProperty("data").GetProperty("maHopDongMoi").GetString()!;
            Assert.Equal(oldCode + "-GH1", newCode);
            var old = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.FirstAsync(h => h.HopDongId == world.ContractA));
            var renewed = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.FirstAsync(h => h.MaHopDong == newCode));
            Assert.Equal(TrangThaiHopDong.DaKetThuc, old.TrangThaiHopDong);
            Assert.Equal(new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc), old.ThoiDiemKetThuc);
            Assert.Equal(TrangThaiHopDong.DangHoatDong, renewed.TrangThaiHopDong);
            Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), renewed.ThoiDiemBatDau);

            var detail = await HtmlTextAsync(await client.GetAsync($"/QuanLyNhaTro/HopDong/ChiTiet/{world.ContractA}"));
            Assert.Contains("Đã gia hạn → " + newCode, detail);
            Assert.Contains(oldCode, detail);
        }

        [Fact]
        public async Task GiaHan_StaffOtherBranch_Is403()
        {
            var world = await PrepareAsync();
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/QuanLyNhaTro/HopDong/GiaHan/{world.ContractB}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Renew(client, token, world.ContractB)).StatusCode);
        }

        [Fact]
        public async Task GiaHan_WithoutAntiforgery_Is400()
        {
            var world = await PrepareAsync();
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await Renew(client, string.Empty, world.ContractA)).StatusCode);
        }

        [Fact]
        public async Task GiaHan_InvoiceExistsInNewMonth_Is400()
        {
            var world = await PrepareAsync();
            await AddInvoiceAsync(world.ContractA, 11, 2026, 2_000_000m, TrangThaiPhatHanhHoaDon.Nhap);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await Renew(client, token, world.ContractA)).StatusCode);
        }

        [Fact]
        public async Task EditForm_NoLongerOffersDaKetThuc_AndServerRejectsIt()
        {
            var world = await PrepareAsync();
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            var html = await HtmlTextAsync(await client.GetAsync(Page));
            var select = Regex.Match(html, @"<select[^>]*id=""TrangThaiHopDong""[^>]*>.*?</select>", RegexOptions.Singleline);
            Assert.True(select.Success);
            Assert.DoesNotContain("value=\"2\"", select.Value);

            var res = await BranchScopeKit.SendJsonAsync(client, token, HttpMethod.Put, $"/HopDong/Update/{world.ContractA}", new
            {
                phongTroId = world.RoomA,
                nguoiThueId = world.TenantA,
                thoiDiemBatDau = "2026-01-01",
                thoiDiemKetThuc = "2026-10-31",
                tienThuePhong = 3_000_000m,
                tienCocPhong = 3_000_000m,
                trangThaiHopDong = 2
            });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var status = await BranchScopeKit.ReadAsync(_factory, db => db.HopDongs.Where(h => h.HopDongId == world.ContractA).Select(h => h.TrangThaiHopDong).FirstAsync());
            Assert.Equal(TrangThaiHopDong.DangHoatDong, status);
        }

        [Fact]
        public async Task TenantOfEndedContract_StillSeesAndCanPayDebt()
        {
            var world = await PrepareAsync();
            var invoiceId = await AddInvoiceAsync(world.ContractA, 10, 2026, 1_500_000m);
            var tenantUser = $"kt_{Guid.NewGuid():N}"[..14];
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.HopDongs.FirstAsync(h => h.HopDongId == world.ContractA)).TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
                await db.SaveChangesAsync();
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = tenantUser, MatKhau = BranchScopeKit.Password, Role = AppRole.KhachThue, NguoiThueId = world.TenantA })).Success);
            }

            var (client, _) = await BranchScopeKit.LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            var panel = await client.GetAsync($"/KhachThue/HoaDon/ThanhToan/{invoiceId}");

            Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
            var data = (await BranchScopeKit.JsonAsync(panel)).GetProperty("data");
            Assert.True(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Equal(1_500_000m, data.GetProperty("conLai").GetDecimal());
            var qr = await client.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{invoiceId}");
            Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
            Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task OffsetLedger_VoidByAdmin_IsRejected()
        {
            var world = await PrepareAsync();
            var invoiceId = await AddInvoiceAsync(world.ContractA, 10, 2026, 2_000_000m);
            await AddLedgerAsync(invoiceId, 500_000m, PhuongThucThanhToan.CanTruTienCoc);
            var ledgerId = await BranchScopeKit.ReadAsync(_factory, db => db.LichSuThanhToans.Where(l => l.HoaDonId == invoiceId).Select(l => l.LichSuThanhToanId).FirstAsync());
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            var res = await BranchScopeKit.SendJsonAsync(client, token, HttpMethod.Delete, $"/LichSuThanhToan/HuyThanhToan/{ledgerId}", new { lyDo = "Thử hủy" });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var deleted = await BranchScopeKit.ReadAsync(_factory, db => db.LichSuThanhToans.Where(l => l.LichSuThanhToanId == ledgerId).Select(l => l.IsDeleted).FirstAsync());
            Assert.False(deleted);
        }
    }
}
