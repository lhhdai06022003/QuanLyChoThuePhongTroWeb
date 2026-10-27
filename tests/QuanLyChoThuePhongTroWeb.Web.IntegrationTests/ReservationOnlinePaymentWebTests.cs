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

    private async Task<string> StaticAsync(string path)
        => await (await _factory.CreateClient().GetAsync(path)).Content.ReadAsStringAsync();

    [Fact]
    public async Task GuestDatCocScript_EscapesByDesign_ChecksTheUrlBeforeRedirecting_AndGuardsDoubleClicks()
    {
        var js = await StaticAsync("/js/guest-dat-coc.js");

        // Id đi vào URL qua encodeURIComponent, chỉ chuyển trang khi link là https và server báo thành công.
        Assert.Contains("encodeURIComponent(reservationId)", js);
        Assert.Contains("res.success", js);
        Assert.Contains("res.url.indexOf('https://') === 0", js);
        Assert.Single(Regex.Matches(js, @"location\.assign\("));

        // Chống bấm đúp: bỏ qua khi nút đang disabled, disable + spinner khi gửi, hoàn nút khi lỗi.
        Assert.Contains("$button.prop('disabled')", js);
        Assert.Contains("spinner-border", js);
        Assert.Contains("prop('disabled', false)", js);

        // Lỗi không im lặng: có .fail và Swal; thông báo server chỉ vào text.
        Assert.Contains(".fail(", js);
        Assert.Contains("Swal.fire", js);
        Assert.Contains("text: message", js);

        // Mọi lần gọi .html( chỉ nhận chuỗi cố định hoặc html gốc của nút, không nhận dữ liệu server.
        foreach (Match call in Regex.Matches(js, @"\.html\(([^;]*)\)"))
        {
            var argument = call.Groups[1].Value.Trim();
            Assert.True(argument.Length == 0 || argument.StartsWith("originalHtml") || argument.StartsWith("'<span"),
                "Gọi .html( với dữ liệu không phải chuỗi cố định: " + call.Value);
        }

        Assert.DoesNotContain("res.message +", js);
        Assert.DoesNotContain("+ res.message", js);
        Assert.DoesNotContain("innerHTML", js);
        Assert.DoesNotContain("document.write", js);

        // Token chỉ ở header, không nằm trên URL.
        Assert.DoesNotContain("?__RequestVerificationToken", js);

        // Nghe sự kiện SignalR đúng tên nghiệp vụ giữ chỗ
        Assert.Contains("GiuChoThanhToanCapNhat", js);
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
