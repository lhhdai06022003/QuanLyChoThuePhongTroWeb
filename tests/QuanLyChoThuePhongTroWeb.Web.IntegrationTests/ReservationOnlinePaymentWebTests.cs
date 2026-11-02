using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public class ReservationOnlinePaymentWebTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReservationOnlinePaymentWebTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestDatCocScript_IsPubliclyServedWithItsPageInitializer()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/js/guest-dat-coc.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType ?? "");
        var js = await response.Content.ReadAsStringAsync();
        Assert.Contains("window.GuestDatCoc =", js);
        // Checkout behavior and failure handling run in tests/browser/guest-dat-coc.test.cjs.
    }

    [Fact]
    public async Task Anonymous_CallingDatCocVnPay_IsRedirectedToLoginOrUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsync("/tai-khoan/yeu-cau-phong/giu-cho/999/dat-coc/vnpay", null);

        // Cookie auth đưa khách chưa đăng nhập về trang đăng nhập (302) hoặc trả 401
        Assert.True(res.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
        if (res.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task DatCocVnPayKetQua_Anonymous_IsRedirectedToLoginOrUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/tai-khoan/yeu-cau-phong/giu-cho/dat-coc/vnpay/ket-qua");

        Assert.True(res.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
        if (res.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task DatCocQr_Anonymous_IsRedirectedToLoginOrUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/tai-khoan/yeu-cau-phong/giu-cho/999/dat-coc/qr");

        Assert.True(res.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized);
        if (res.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location?.ToString() ?? string.Empty);
        }
    }
}
