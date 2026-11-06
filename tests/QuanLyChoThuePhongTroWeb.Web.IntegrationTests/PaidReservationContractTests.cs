using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class PaidReservationContractTests(CustomWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly string suffix = Guid.NewGuid().ToString("N")[..8];
    private readonly string cccd = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
    private ChiNhanh branch = null!;
    private PhongTro room = null!;
    private NguoiDung admin = null!;
    private NguoiDung staff = null!;
    private NguoiDung booker = null!;
    private KhachVangLai guest = null!;
    private YeuCauGiuCho hold = null!;
    private YeuCauThanhToanGiuCho payment = null!;
    private NguoiThue? otherTenant;
    private NguoiDung? otherUser;

    private string DetailPath => $"/QuanLyNhaTro/GiuCho/ChiTiet/{hold.YeuCauGiuChoId}";
    private string ContractPath => $"/QuanLyNhaTro/GiuCho/XacNhanThue/{hold.YeuCauGiuChoId}";
    private const string ClosePath = "/QuanLyNhaTro/GiuCho/KhachKhongThue";
    private const string ExpiredPath = "/QuanLyNhaTro/GiuCho/HuyQuaHanKy";
    private const string HistoryPath = "/tai-khoan/yeu-cau-phong";
    private const string ClosureReason = "Khách báo không thuê";

    [Fact]
    public async Task NoRentClosesPaidHoldOnceAndReleasesRoomWithoutCreditOrRefund()
    {
        await MarkSelfServiceAsync();
        using var visitor = BranchScopeKit.NewClient(factory);
        var publicPath = $"/phong/{room.PhongTroId}";
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(publicPath)).StatusCode);

        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            var form = ClosureForm();
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ClosePath, form)).StatusCode);
            Assert.Contains("Khách không thuê", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ClosePath, form)).StatusCode);
            await PostRefundAsync(client, token, 300_000m);
            Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
            await PostRefundTransferAsync(client, token, 300_000m);
            Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
        }

        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync(publicPath)).StatusCode);
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            var saved = (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!;
            Assert.Equal("KhachKhongThue", saved.TrangThai.ToString());
            Assert.Equal(ClosureReason, saved.LyDoTuChoiHoacHuy);
            Assert.Equal(300_000m, saved.SoTienGiuCho);
            var history = await db.LichSuTrangThaiYeuCauGiuChos.SingleAsync(h =>
                h.YeuCauGiuChoId == hold.YeuCauGiuChoId && h.TrangThaiTruoc == TrangThaiYeuCauGiuCho.DangGiuCho);
            Assert.Equal(saved.TrangThai, history.TrangThaiSau);
            Assert.Equal(ClosureReason, history.LyDo);
            Assert.Equal(admin.NguoiDungId, history.NguoiThucHienId);
            Assert.False(await db.HopDongs.AnyAsync(h => h.PhongTroId == room.PhongTroId));
            Assert.False(await db.HoaDons.AnyAsync(h => h.HopDong.PhongTroId == room.PhongTroId));
            Assert.False(await db.ApDungTienGiuChoVaoTienCocs.AnyAsync(a => a.YeuCauGiuChoId == hold.YeuCauGiuChoId));
            Assert.Equal(300_000m, (await db.GiaoDichGiuChos.SingleAsync(g =>
                g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId)).SoTienThucNhan);
            var decision = await db.QuyetDinhHoanTienGiuChos.Include(q => q.GiaoDichHoanTiens)
                .SingleAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId);
            Assert.Equal(0m, decision.SoTienHoanDuyet);
            Assert.Equal(TrangThaiHoanTien.DaHoanTien, decision.TrangThai);
            Assert.Empty(decision.GiaoDichHoanTiens);
            return 0;
        });
        var (guestClient, _) = await BranchScopeKit.LoginAsync(factory, booker.TenDangNhap, HistoryPath);
        using (guestClient)
        {
            var history = WebUtility.HtmlDecode(await guestClient.GetStringAsync(HistoryPath));
            Assert.Contains("Cọc giữ lại", history);
            Assert.Contains("300.000 ₫", history);
            var checkoutPath = $"{HistoryPath}/giu-cho/{hold.YeuCauGiuChoId}/dat-coc";
            var checkout = WebUtility.HtmlDecode(await guestClient.GetStringAsync(checkoutPath));
            Assert.Contains("Vui lòng không chuyển thêm tiền", checkout);
            Assert.DoesNotContain($"{checkoutPath}/qr", checkout);
        }
        await AssertReportedRetentionAsync();
    }

    [Fact]
    public async Task NoRentKeepsExcessAndSubsequentLateTransferSeparateFromRetainedDeposit()
    {
        await MarkSelfServiceAsync();
        await BranchScopeKit.ReadAsync(factory, db =>
        {
            db.GiaoDichGiuChos.Add(Receipt(50_000m, "excess"));
            return db.SaveChangesAsync();
        });
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            await PostRefundAsync(client, token, 50_000m);
            await PostRefundTransferAsync(client, token, 50_000m);
            Assert.Equal(0m, (await GetRevenueReportAsync()).Tong.ThuKhacGiuCho);
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ClosePath, ClosureForm())).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IReservationAutoPaymentStore>();
                var now = DateTime.UtcNow;
                var result = await store.RecordSystemTransferAsync(payment.MaYeuCau, $"late-{suffix}", 25_000m, now, "Test", now);
                Assert.True(result.CanHoanTien);
            }
            await AssertReportedRetentionAsync();
            await PostRefundAsync(client, token, 75_000m);
            await PostRefundTransferAsync(client, token, 25_000m, "late");
            await AssertReportedRetentionAsync();
        }
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            var decision = await db.QuyetDinhHoanTienGiuChos.Include(q => q.GiaoDichHoanTiens)
                .SingleAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId);
            Assert.Equal(75_000m, decision.SoTienHoanDuyet);
            Assert.Equal(new[] { 25_000m, 50_000m }, decision.GiaoDichHoanTiens.Select(g => g.SoTienHoan).Order().ToArray());
            Assert.Equal(3, await db.GiaoDichGiuChos.CountAsync(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId));
            return 0;
        });
    }

    [Fact]
    public async Task PaidSelfServiceRejectsGuestCancelManualRefundAndPrematureExpiredClosure()
    {
        await MarkSelfServiceAsync();
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            await PostRefundAsync(client, token, 300_000m);
            Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token,
                ClosePath, ClosureForm(confirm: false))).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ExpiredPath, ClosureForm())).StatusCode);
            var (guestClient, guestToken) = await BranchScopeKit.LoginAsync(factory, booker.TenDangNhap, HistoryPath);
            using (guestClient)
            {
                var cancelPath = $"{HistoryPath}/giu-cho/{hold.YeuCauGiuChoId}/huy";
                Assert.DoesNotContain(cancelPath, await guestClient.GetStringAsync(HistoryPath));
                Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(guestClient, guestToken,
                    cancelPath, new Dictionary<string, string>())).StatusCode);
            }
            await AssertUnconvertedAsync();
            await ExpireSigningDeadlineAsync();
            var unconfirmed = ClosureForm();
            unconfirmed.Remove("confirm");
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ExpiredPath, unconfirmed)).StatusCode);
            await AssertUnconvertedAsync();
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ExpiredPath, ClosureForm())).StatusCode);
            Assert.Contains("Khách không thuê", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
        }
        await AssertReportedRetentionAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialOrLateOnlyTransferDoesNotQualifyAsPaidDeposit(bool lateOnly)
    {
        await MarkSelfServiceAsync();
        await BranchScopeKit.ReadAsync(factory, db => db.GiaoDichGiuChos
            .Where(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.SoTienThucNhan, lateOnly ? 300_000m : 150_000m)
                .SetProperty(g => g.GhiChu, lateOnly ? ReservationPaymentMarkers.LateRefundRequired : null)));
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            Assert.DoesNotContain(ClosePath, await client.GetStringAsync(DetailPath));
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ClosePath, ClosureForm())).StatusCode);
            Assert.Contains("Chưa nhận đủ tiền", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
        }
        await AssertUnconvertedAsync();
    }

    [Fact]
    public async Task UnpaidSelfServiceShowsDepositPolicyAndStillAllowsGuestToCancel()
    {
        await MarkSelfServiceAsync();
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            var savedHold = (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!;
            savedHold.TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan;
            savedHold.HanThanhToan = DateTime.UtcNow.AddMinutes(10);
            var savedPayment = (await db.YeuCauThanhToanGiuChos.FindAsync(payment.YeuCauThanhToanGiuChoId))!;
            savedPayment.HanThanhToan = savedHold.HanThanhToan.Value;
            savedPayment.TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan;
            await db.GiaoDichGiuChos.Where(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId).ExecuteDeleteAsync();
            return await db.SaveChangesAsync();
        });
        var (client, token) = await BranchScopeKit.LoginAsync(factory, booker.TenDangNhap, HistoryPath);
        using (client)
        {
            var bookingPath = $"/phong/{room.PhongTroId}/giu-cho";
            var checkoutPath = $"{HistoryPath}/giu-cho/{hold.YeuCauGiuChoId}/dat-coc";
            Assert.Contains(SelfServiceReservationRules.DepositPolicy, WebUtility.HtmlDecode(await client.GetStringAsync(bookingPath)));
            Assert.Contains(SelfServiceReservationRules.DepositPolicy, WebUtility.HtmlDecode(await client.GetStringAsync(checkoutPath)));
            var cancelPath = $"{HistoryPath}/giu-cho/{hold.YeuCauGiuChoId}/huy";
            Assert.Contains(cancelPath, await client.GetStringAsync(HistoryPath));
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token,
                cancelPath, new Dictionary<string, string>())).StatusCode);
        }
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy, (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!.TrangThai);
            Assert.False(await db.QuyetDinhHoanTienGiuChos.AnyAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId));
            return 0;
        });
    }

    [Fact]
    public async Task ExpiredHistoricalHoldCanStillRecordPriorRefundDecision()
    {
        var (guestClient, guestToken) = await BranchScopeKit.LoginAsync(factory, booker.TenDangNhap, HistoryPath);
        using (guestClient)
        {
            var cancelPath = $"{HistoryPath}/giu-cho/{hold.YeuCauGiuChoId}/huy";
            Assert.DoesNotContain(cancelPath, await guestClient.GetStringAsync(HistoryPath));
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(guestClient, guestToken,
                cancelPath, new Dictionary<string, string>())).StatusCode);
        }
        await AssertUnconvertedAsync();
        await ExpireSigningDeadlineAsync();
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            var unconfirmed = ClosureForm();
            unconfirmed.Remove("confirm");
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, ExpiredPath, unconfirmed)).StatusCode);
            await PostRefundAsync(client, token, 300_000m);
            await PostRefundTransferAsync(client, token, 300_000m);
        }
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy, (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!.TrangThai);
            var decision = await db.QuyetDinhHoanTienGiuChos.Include(q => q.GiaoDichHoanTiens)
                .SingleAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId);
            Assert.Equal(300_000m, Assert.Single(decision.GiaoDichHoanTiens).SoTienHoan);
            return 0;
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SelfServiceHoldCanCloseUnderEarlierDepositRefund(bool fullyRefunded)
    {
        var approved = fullyRefunded ? 300_000m : 100_000m;
        var expectedRetention = fullyRefunded ? 0m : 220_000m;
        const string partialRetentionText = "Cọc giữ lại: 220.000 ₫";
        if (!fullyRefunded)
        {
            await BranchScopeKit.ReadAsync(factory, db =>
            {
                db.GiaoDichGiuChos.Add(Receipt(20_000m, "prior-excess"));
                return db.SaveChangesAsync();
            });
        }
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            await PostRefundAsync(client, token, approved);
            if (fullyRefunded)
                await PostRefundTransferAsync(client, token, approved, "earlier");

            var prior = await BranchScopeKit.ReadAsync(factory, db => db.QuyetDinhHoanTienGiuChos
                .SingleAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId));
            await MarkSelfServiceAsync();
            await ExpireSigningDeadlineAsync();

            var detail = WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath));
            Assert.Contains(ClosePath, detail);
            Assert.Contains("name=\"preservePriorRefund\" value=\"true\"", detail);
            Assert.Contains("quyết định hoàn tiền trước đó", detail);
            Assert.DoesNotContain("Cọc giữ lại:", detail);

            var path = fullyRefunded ? ClosePath : ExpiredPath;
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, path, ClosureForm())).StatusCode);
            await AssertUnconvertedAsync();

            var recovery = ClosureForm();
            recovery["preservePriorRefund"] = "true";
            Assert.Equal(HttpStatusCode.Redirect, (await BranchScopeKit.PostFormAsync(client, token, path, recovery)).StatusCode);
            Assert.Equal(expectedRetention, (await GetRevenueReportAsync()).Tong.ThuKhacGiuCho);
            detail = WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath));
            Assert.Contains("Đã hủy giữ chỗ theo quyết định hoàn tiền trước đó", detail);
            if (fullyRefunded)
                Assert.DoesNotContain("Cọc giữ lại:", detail);
            else
                Assert.Contains(partialRetentionText, detail);
            await BranchScopeKit.ReadAsync(factory, async db =>
            {
                var saved = (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!;
                Assert.Equal(TrangThaiYeuCauGiuCho.DaHuy, saved.TrangThai);
                Assert.Equal(ClosureReason, saved.LyDoTuChoiHoacHuy);
                var decision = await db.QuyetDinhHoanTienGiuChos.Include(q => q.GiaoDichHoanTiens)
                    .SingleAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId);
                Assert.Equal(prior.QuyetDinhHoanTienGiuChoId, decision.QuyetDinhHoanTienGiuChoId);
                Assert.Equal(prior.SoTienHoanDuyet, decision.SoTienHoanDuyet);
                Assert.Equal(prior.NgayQuyetDinh, decision.NgayQuyetDinh);
                Assert.Equal(prior.LyDo, decision.LyDo);
                Assert.Equal(prior.TrangThai, decision.TrangThai);
                Assert.Equal(fullyRefunded ? approved : 0m, decision.GiaoDichHoanTiens.Sum(g => g.SoTienHoan));
                Assert.Equal(fullyRefunded ? 300_000m : 320_000m, await db.GiaoDichGiuChos
                    .Where(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId)
                    .SumAsync(g => g.SoTienThucNhan));
                return 0;
            });
            if (!fullyRefunded)
            {
                await PostRefundAsync(client, token, 300_000m);
                Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
                await PostRefundAsync(client, token, 50_000m);
                Assert.Contains("Số tiền hoàn thấp hơn", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
                await PostRefundTransferAsync(client, token, approved, "earlier");
            }
            var refund = await BranchScopeKit.ReadAsync(factory, db => db.GiaoDichHoanTienGiuChos
                .SingleAsync(g => g.QuyetDinhHoanTienGiuChoId == prior.QuyetDinhHoanTienGiuChoId));
            Assert.Equal(approved, refund.SoTienHoan);

            if (!fullyRefunded)
            {
                using (var scope = factory.Services.CreateScope())
                {
                    var store = scope.ServiceProvider.GetRequiredService<IReservationAutoPaymentStore>();
                    var now = DateTime.UtcNow;
                    var result = await store.RecordSystemTransferAsync(payment.MaYeuCau, $"late-{suffix}", 25_000m, now, "Test", now);
                    Assert.True(result.CanHoanTien);
                }
                await PostRefundAsync(client, token, 145_000m);
                Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
                await PostRefundAsync(client, token, 125_000m);
                await PostRefundAsync(client, token, 145_000m);
                Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
                await PostRefundTransferAsync(client, token, 26_000m, "too-much");
                Assert.Contains("Số tiền vượt", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
                await PostRefundTransferAsync(client, token, 25_000m, "late");
                await BranchScopeKit.ReadAsync(factory, async db =>
                {
                    var decision = await db.QuyetDinhHoanTienGiuChos.Include(q => q.GiaoDichHoanTiens)
                        .SingleAsync(q => q.QuyetDinhHoanTienGiuChoId == prior.QuyetDinhHoanTienGiuChoId);
                    Assert.Equal(125_000m, decision.SoTienHoanDuyet);
                    Assert.Equal(TrangThaiHoanTien.DaHoanTien, decision.TrangThai);
                    Assert.Equal(new[] { 25_000m, 100_000m }, decision.GiaoDichHoanTiens.Select(g => g.SoTienHoan).Order().ToArray());
                    // Move the completed decision to a different report period from the closure.
                    decision.NgayQuyetDinh = DateTime.UtcNow.AddMonths(1);
                    return await db.SaveChangesAsync();
                });
                Assert.Equal(0m, (await GetRevenueReportAsync(monthsFromNow: 1)).Tong.ThuKhacGiuCho);
            }
        }

        using var visitor = BranchScopeKit.NewClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/phong/{room.PhongTroId}")).StatusCode);
        var (guestClient, _) = await BranchScopeKit.LoginAsync(factory, booker.TenDangNhap, HistoryPath);
        using (guestClient)
        {
            var history = WebUtility.HtmlDecode(await guestClient.GetStringAsync(HistoryPath));
            Assert.Contains("Quản lý đã hủy giữ chỗ theo quyết định hoàn tiền trước đó", history);
            if (fullyRefunded)
                Assert.DoesNotContain("Cọc giữ lại:", history);
            else
                Assert.Contains(partialRetentionText, history);
        }
        Assert.Equal(expectedRetention, (await GetRevenueReportAsync()).Tong.ThuKhacGiuCho);
    }

    private Task<int> ExpireSigningDeadlineAsync() => BranchScopeKit.ReadAsync(factory,
        db => db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == hold.YeuCauGiuChoId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.HanKyHopDong, DateTime.UtcNow.AddMinutes(-1))));

    private Dictionary<string, string> ClosureForm(bool confirm = true) => new()
    {
        ["reservationId"] = hold.YeuCauGiuChoId.ToString(), ["reason"] = ClosureReason, ["confirm"] = confirm.ToString()
    };

    private Task<HttpResponseMessage> PostRefundAsync(HttpClient client, string token, decimal amount) =>
        BranchScopeKit.PostFormAsync(client, token, "/QuanLyNhaTro/GiuCho/QuyetDinhHoan", new Dictionary<string, string>
        {
            ["reservationId"] = hold.YeuCauGiuChoId.ToString(), ["reason"] = "Hoàn phần chuyển thừa", ["amount"] = amount.ToString()
        });

    private Task<HttpResponseMessage> PostRefundTransferAsync(HttpClient client, string token, decimal amount, string tag = "excess") =>
        BranchScopeKit.PostFormAsync(client, token, "/QuanLyNhaTro/GiuCho/XacNhanHoan", new Dictionary<string, string>
        {
            ["reservationId"] = hold.YeuCauGiuChoId.ToString(), ["bankReference"] = $"refund-{tag}-{suffix}", ["amount"] = amount.ToString()
        });

    private async Task AssertReportedRetentionAsync()
    {
        var report = await GetRevenueReportAsync();
        Assert.Equal(300_000m, report.Tong.ThuKhacGiuCho);
        Assert.Equal(0m, report.TienCocDangGiu);
        Assert.Equal(0, report.Tong.SoHoaDon);
    }

    private async Task<BaoCaoDoanhThuDto> GetRevenueReportAsync(int monthsFromNow = 0)
    {
        using var scope = factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IBaoCaoService>();
        var localNow = DateTime.UtcNow.AddHours(7).AddMonths(monthsFromNow);
        var report = await reports.GetDoanhThuAsync(admin.NguoiDungId, new BaoCaoFilter
        {
            ChiNhanhId = branch.ChiNhanhId, TuThang = localNow.Month, DenThang = localNow.Month,
            TuNam = localNow.Year, DenNam = localNow.Year
        });
        Assert.True(report.Success, report.Message);
        return report.Data!;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ConfirmationUsesBookingAccountAndAppliesOnlyThePaidDeposit(bool alreadyLinked, bool unownedProfile)
    {
        await MarkSelfServiceAsync();
        if (alreadyLinked || unownedProfile)
            await AddExistingTenantAsync(linkToBooker: alreadyLinked, ownedByOther: false);
        await AddAdditionalReceiptAsync();
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            Assert.Contains(ContractPath, await client.GetStringAsync(DetailPath));
            var draft = WebUtility.HtmlDecode(await client.GetStringAsync(ContractPath));
            Assert.Contains(room.SoPhong, draft);
            Assert.Contains(guest.HoTen, draft);
            Assert.Contains(guest.Email!, draft);
            Assert.Contains(guest.SoDienThoai, draft);
            Assert.Contains("300.000 ₫", draft);
            Assert.Contains("2500000", draft);
            Assert.Equal(!alreadyLinked, draft.Contains("name=\"Input.CCCD\""));

            var form = ContractForm(alreadyLinked ? "123" : cccd);
            // Extra fields cannot override the booking identity, room or paid amount.
            form["Input.PhongTroId"] = "999999";
            form["Input.NguoiThueId"] = "999999";
            form["Input.TienCocPhong"] = "1";
            var confirmation = await BranchScopeKit.PostFormAsync(client, token, ContractPath, form);
            Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);
            var repeat = await BranchScopeKit.PostFormAsync(client, token, ContractPath, form);
            Assert.Equal(HttpStatusCode.Redirect, repeat.StatusCode);
        }

        var contract = await BranchScopeKit.ReadAsync(factory, async db =>
        {
            var saved = await db.HopDongs.Include(h => h.NguoiThue).Include(h => h.PhongTro)
                .SingleAsync(h => h.PhongTroId == room.PhongTroId);
            Assert.Equal(300_000m, saved.TienCocPhong);
            Assert.Equal(2_400_000m, saved.TienThuePhong);
            Assert.Equal(new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc), saved.ThoiDiemBatDau);
            Assert.Equal(new DateTime(2027, 11, 6, 0, 0, 0, DateTimeKind.Utc), saved.ThoiDiemKetThuc);
            Assert.Equal(TrangThaiPhong.DaThue, saved.PhongTro.TrangThai);
            Assert.Equal(guest.HoTen, saved.NguoiThue.HoVaTen);
            Assert.Equal(guest.Email, saved.NguoiThue.Email);
            Assert.Equal(guest.SoDienThoai, saved.NguoiThue.SoDienThoai);
            Assert.Equal(cccd, saved.NguoiThue.CCCD);
            if (alreadyLinked || unownedProfile)
                Assert.Equal(otherTenant!.NguoiThueId, saved.NguoiThueId);
            var applied = await db.ApDungTienGiuChoVaoTienCocs.SingleAsync(a => a.YeuCauGiuChoId == hold.YeuCauGiuChoId);
            Assert.Equal(saved.HopDongId, applied.HopDongId);
            Assert.Equal(300_000m, applied.SoTienApDung);
            Assert.Equal(admin.NguoiDungId, applied.NguoiThucHienId);
            Assert.Equal(TrangThaiYeuCauGiuCho.DaChuyenHopDong,
                (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!.TrangThai);
            Assert.Equal(TrangThaiYeuCauGiuCho.DaChuyenHopDong,
                (await db.LichSuTrangThaiYeuCauGiuChos.SingleAsync(h => h.YeuCauGiuChoId == hold.YeuCauGiuChoId &&
                    h.TrangThaiTruoc == TrangThaiYeuCauGiuCho.DangGiuCho)).TrangThaiSau);
            var account = await db.NguoiDungs.SingleAsync(u => u.NguoiThueId == saved.NguoiThueId);
            Assert.Equal(booker.NguoiDungId, account.NguoiDungId);
            Assert.Equal(booker.TenDangNhap, account.TenDangNhap);
            Assert.Equal(booker.MatKhauHash, account.MatKhauHash);
            Assert.Equal(Role.KhachThue, account.Role);
            Assert.Equal(3, await db.GiaoDichGiuChos.CountAsync(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId));
            Assert.Equal(75_000m, (await db.QuyetDinhHoanTienGiuChos.SingleAsync(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId)).SoTienHoanDuyet);
            return saved;
        });

        using var customer = BranchScopeKit.NewClient(factory);
        const string loginPath = "/QuanLyNhaTro/DangNhap";
        var loginToken = BranchScopeKit.Token(await customer.GetStringAsync(loginPath));
        var login = await BranchScopeKit.PostFormAsync(customer, loginToken, loginPath, new Dictionary<string, string>
        {
            ["TenDangNhap"] = booker.TenDangNhap,
            ["MatKhau"] = BranchScopeKit.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains(contract.MaHopDong, await customer.GetStringAsync("/KhachThue/HopDong/GetDanhSach"));
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/KhachThue/HoaDon/GetDanhSach")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyApplyCannotBypassBookingAccountForSelfService(bool selfService)
    {
        if (selfService)
            await MarkSelfServiceAsync();
        await AddExistingTenantAsync(linkToBooker: false, ownedByOther: true);
        var contractId = await AddActiveContractAsync();
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            var response = await BranchScopeKit.PostFormAsync(client, token, "/QuanLyNhaTro/GiuCho/ApDungCoc",
                new Dictionary<string, string>
                {
                    ["reservationId"] = hold.YeuCauGiuChoId.ToString(),
                    ["contractId"] = contractId.ToString(), ["amount"] = "300000"
                });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            if (selfService)
            {
                await AssertUnconvertedAsync(contractId);
                Assert.DoesNotContain("/QuanLyNhaTro/GiuCho/ApDungCoc", await client.GetStringAsync(DetailPath));
            }
            else
            {
                var application = await BranchScopeKit.ReadAsync(factory, db => db.ApDungTienGiuChoVaoTienCocs
                    .SingleAsync(a => a.YeuCauGiuChoId == hold.YeuCauGiuChoId));
                Assert.Equal(contractId, application.HopDongId);
                var replay = await BranchScopeKit.PostFormAsync(client, token, ContractPath, ContractForm(cccd));
                Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
                Assert.Contains("chưa liên kết với tài khoản đặt chỗ", WebUtility.HtmlDecode(await client.GetStringAsync(DetailPath)));
            }
        }
    }

    [Fact]
    public async Task StaffOutsideBranchCannotOpenOrSubmitConfirmation()
    {
        var (client, token) = await BranchScopeKit.LoginAsync(factory, staff.TenDangNhap, "/QuanLyNhaTro/GiuCho");
        using (client)
        {
            var get = await client.GetAsync(ContractPath);
            Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
            Assert.StartsWith("/QuanLyNhaTro/DangNhap", get.Headers.Location?.AbsolutePath);
            var post = await BranchScopeKit.PostFormAsync(client, token, ContractPath, ContractForm(cccd));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.StartsWith("/QuanLyNhaTro/DangNhap", post.Headers.Location?.AbsolutePath);
            var closure = await BranchScopeKit.PostFormAsync(client, token, ClosePath, ClosureForm());
            Assert.Equal(HttpStatusCode.Redirect, closure.StatusCode);
            Assert.StartsWith("/QuanLyNhaTro/DangNhap", closure.Headers.Location?.AbsolutePath);
        }
        await AssertUnconvertedAsync();
    }

    [Fact]
    public async Task MatchingCccdCannotClaimAProfileOwnedByAnotherAccount()
    {
        await AddExistingTenantAsync(linkToBooker: false, ownedByOther: true);
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            var post = await BranchScopeKit.PostFormAsync(client, token, ContractPath, ContractForm(cccd));
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            Assert.Contains("CCCD hoặc thông tin liên hệ trùng", WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync()));
        }
        await AssertUnconvertedAsync();
        Assert.Equal(otherTenant!.NguoiThueId, await BranchScopeKit.ReadAsync(factory, async db =>
            (await db.NguoiDungs.FindAsync(otherUser!.NguoiDungId))!.NguoiThueId));
    }

    [Fact]
    public async Task ActiveRoomContractRollsBackPreparationOfBookingAccountAndTenant()
    {
        otherTenant = new NguoiThue
        {
            HoVaTen = "Người thuê khác", Email = $"other-{suffix}@example.test",
            SoDienThoai = "08" + Random.Shared.Next(10_000_000, 99_999_999),
            CCCD = "0" + cccd[..11]
        };
        var existingId = await AddActiveContractAsync();
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            var response = await BranchScopeKit.PostFormAsync(client, token, ContractPath, ContractForm(cccd));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("hợp đồng hiệu lực", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }

        await AssertUnconvertedAsync(existingId);
        Assert.False(await BranchScopeKit.ReadAsync(factory, db => db.NguoiThues.AnyAsync(t => t.CCCD == cccd)));
    }

    private Task<int> MarkSelfServiceAsync() => BranchScopeKit.ReadAsync(factory, db =>
    {
        db.LichSuTrangThaiYeuCauGiuChos.Add(new LichSuTrangThaiYeuCauGiuCho
        {
            YeuCauGiuChoId = hold.YeuCauGiuChoId, TrangThaiSau = TrangThaiYeuCauGiuCho.ChoThanhToan,
            LoaiTacNhan = LoaiTacNhan.NguoiDung, NguoiThucHienId = booker.NguoiDungId,
            NgayThucHien = hold.NgayTao, LyDo = SelfServiceReservationRules.CreationReason
        });
        return db.SaveChangesAsync();
    });

    private Task<int> AddActiveContractAsync() => BranchScopeKit.ReadAsync(factory, async db =>
    {
        var existing = new HopDong
        {
            NguoiThueId = otherTenant!.NguoiThueId, PhongTroId = room.PhongTroId,
            MaHopDong = $"Existing-{suffix}", TienCocPhong = 300_000m, TienThuePhong = 2_500_000m,
            ThoiDiemBatDau = DateTime.UtcNow, TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
        };
        if (otherTenant.NguoiThueId == 0)
            existing.NguoiThue = otherTenant;
        db.HopDongs.Add(existing);
        await db.SaveChangesAsync();
        return existing.HopDongId;
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidDepositCannotBeApplied(bool refundConsumesDeposit)
    {
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            if (refundConsumesDeposit)
                AddRefundDecision(db, 300_000m);
            else
                await db.GiaoDichGiuChos.Where(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.GhiChu, ReservationPaymentMarkers.LateRefundRequired));
            return await db.SaveChangesAsync();
        });
        var (client, token) = await BranchScopeKit.LoginAsync(factory, admin.TenDangNhap, DetailPath);
        using (client)
        {
            var post = await BranchScopeKit.PostFormAsync(client, token, ContractPath, ContractForm(cccd));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Equal(DetailPath, post.Headers.Location?.OriginalString);
        }
        await AssertUnconvertedAsync();
    }

    private static Dictionary<string, string> ContractForm(string cccd) => new()
    {
        ["Input.CCCD"] = cccd,
        ["Input.StartDate"] = "2026-11-06",
        ["Input.EndDate"] = "2027-11-06",
        ["Input.Rent"] = "2400000"
    };

    private async Task AssertUnconvertedAsync(int? existingContractId = null)
    {
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            Assert.False(await db.HopDongs.AnyAsync(h => h.PhongTroId == room.PhongTroId && h.HopDongId != existingContractId));
            Assert.False(await db.ApDungTienGiuChoVaoTienCocs.AnyAsync(a => a.YeuCauGiuChoId == hold.YeuCauGiuChoId));
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, (await db.YeuCauGiuChos.FindAsync(hold.YeuCauGiuChoId))!.TrangThai);
            Assert.Equal(TrangThaiPhong.Trong, (await db.PhongTros.FindAsync(room.PhongTroId))!.TrangThai);
            var account = (await db.NguoiDungs.FindAsync(booker.NguoiDungId))!;
            Assert.Null(account.NguoiThueId);
            Assert.Equal(Role.KhachVangLai, account.Role);
            return 0;
        });
    }

    private async Task AddExistingTenantAsync(bool linkToBooker, bool ownedByOther)
    {
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            otherTenant = new NguoiThue { HoVaTen = guest.HoTen, Email = guest.Email!, SoDienThoai = guest.SoDienThoai, CCCD = cccd };
            db.NguoiThues.Add(otherTenant);
            if (linkToBooker)
            {
                var user = (await db.NguoiDungs.FindAsync(booker.NguoiDungId))!;
                user.NguoiThue = otherTenant;
                user.Role = Role.KhachThue;
            }
            else if (ownedByOther)
            {
                otherUser = NewUser("other", Role.KhachThue, booker.MatKhauHash);
                otherUser.NguoiThue = otherTenant;
                db.NguoiDungs.Add(otherUser);
            }
            return await db.SaveChangesAsync();
        });
    }

    private async Task AddAdditionalReceiptAsync()
    {
        await BranchScopeKit.ReadAsync(factory, async db =>
        {
            db.GiaoDichGiuChos.AddRange(Receipt(50_000m, "excess"), Receipt(25_000m, "late", late: true));
            AddRefundDecision(db, 75_000m);
            return await db.SaveChangesAsync();
        });
    }

    private void AddRefundDecision(ApplicationDbContext db, decimal amount) =>
        db.QuyetDinhHoanTienGiuChos.Add(new QuyetDinhHoanTienGiuCho
        {
            YeuCauGiuChoId = hold.YeuCauGiuChoId, SoTienHoanDuyet = amount,
            NguoiQuyetDinhId = admin.NguoiDungId, LyDo = "Hoàn theo giao dịch"
        });

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordService>().HashPassword(BranchScopeKit.Password);
        admin = NewUser("admin", Role.Admin, hash);
        staff = NewUser("staff", Role.NhanVien, hash);
        booker = NewUser("booker", Role.KhachVangLai, hash);
        db.NguoiDungs.AddRange(admin, staff, booker);
        branch = new ChiNhanh { MaChiNhanh = TestCodes.MaChiNhanh("C"), TenChiNhanh = "Chi nhánh " + suffix, DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001" };
        room = new PhongTro
        {
            ChiNhanh = branch, SoPhong = "P" + suffix, GiaThue = 2_500_000m, TienCocPhong = 300_000m,
            TrangThai = TrangThaiPhong.Trong, DuocDangTin = true, MoTa = "Test", DienTich = 20, SoNguoiToiDa = 2
        };
        guest = new KhachVangLai
        {
            NguoiDung = booker, HoTen = "Khách đặt " + suffix, Email = $"{suffix}@example.test",
            SoDienThoai = "09" + Random.Shared.Next(10_000_000, 99_999_999), DaXacMinhEmail = true
        };
        var now = DateTime.UtcNow;
        hold = new YeuCauGiuCho
        {
            PhongTro = room, KhachVangLai = guest, TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho,
            SoTienGiuCho = 300_000m, NgayTao = now.AddHours(-4), HanThanhToan = now.AddHours(-1), HanKyHopDong = now.AddDays(1)
        };
        payment = new YeuCauThanhToanGiuCho
        {
            YeuCauGiuCho = hold, NguoiTao = booker, MaYeuCau = "GC" + suffix, NoiDungChuyenKhoan = "GC" + suffix,
            SoTien = 300_000m, NgayTao = hold.NgayTao, HanThanhToan = hold.HanThanhToan.Value,
            TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat
        };
        db.YeuCauThanhToanGiuChos.Add(payment);
        await db.SaveChangesAsync();
        db.GiaoDichGiuChos.Add(Receipt(300_000m, "deposit"));
        await db.SaveChangesAsync();
    }

    private NguoiDung NewUser(string prefix, Role role, string hash) => new()
    {
        TenDangNhap = $"conversion_{prefix}_{suffix}", Role = role, MatKhauHash = hash
    };

    private GiaoDichGiuCho Receipt(decimal amount, string label, bool late = false) => new()
    {
        YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId, SoTienThucNhan = amount,
        MaGiaoDich = $"{label}-{suffix}", PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
        NgayThucNhan = payment.HanThanhToan.AddMinutes(late ? 1 : -1),
        GhiChu = late ? ReservationPaymentMarkers.LateRefundRequired : null
    };

    public async Task DisposeAsync()
    {
        if (hold?.YeuCauGiuChoId is not > 0)
            return;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contracts = db.HopDongs.Where(h => h.PhongTroId == room.PhongTroId);
        var contractIds = contracts.Select(h => h.HopDongId);
        await db.ApDungTienGiuChoVaoTienCocs.Where(a => a.YeuCauGiuChoId == hold.YeuCauGiuChoId).ExecuteDeleteAsync();
        await db.ChiTietThanhVienHopDongs.Where(m => contractIds.Contains(m.HopDongId)).ExecuteDeleteAsync();
        await contracts.ExecuteDeleteAsync();
        await db.GiaoDichHoanTienGiuChos.Where(g => g.QuyetDinhHoanTienGiuCho.YeuCauGiuChoId == hold.YeuCauGiuChoId).ExecuteDeleteAsync();
        await db.QuyetDinhHoanTienGiuChos.Where(q => q.YeuCauGiuChoId == hold.YeuCauGiuChoId).ExecuteDeleteAsync();
        await db.GiaoDichGiuChos.Where(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId).ExecuteDeleteAsync();
        await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(h => h.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId).ExecuteDeleteAsync();
        await db.YeuCauThanhToanGiuChos.Where(p => p.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId).ExecuteDeleteAsync();
        await db.LichSuTrangThaiYeuCauGiuChos.Where(h => h.YeuCauGiuChoId == hold.YeuCauGiuChoId).ExecuteDeleteAsync();
        await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == hold.YeuCauGiuChoId).ExecuteDeleteAsync();
        await db.KhachVangLais.Where(k => k.KhachVangLaiId == guest.KhachVangLaiId).ExecuteDeleteAsync();
        var userIds = new[] { admin.NguoiDungId, staff.NguoiDungId, booker.NguoiDungId, otherUser?.NguoiDungId ?? 0 };
        await db.NguoiDungs.Where(u => userIds.Contains(u.NguoiDungId)).ExecuteDeleteAsync();
        var existingTenantId = otherTenant?.NguoiThueId;
        await db.NguoiThues.Where(t => t.CCCD == cccd || t.NguoiThueId == existingTenantId).ExecuteDeleteAsync();
        await db.PhongTros.Where(r => r.PhongTroId == room.PhongTroId).ExecuteDeleteAsync();
        await db.ChiNhanhs.Where(b => b.ChiNhanhId == branch.ChiNhanhId).ExecuteDeleteAsync();
    }
}
