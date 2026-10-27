using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers;

public static class PaymentWebHelper
{
    // IP kết nối của khách (spec VNPay §13). Thiếu hoặc IPv6 loopback thì dùng 127.0.0.1 vì VNPay chỉ nhận IPv4 hoặc IPv6 đầy đủ.
    public static string ResolveClientIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address == null || (IPAddress.IsLoopback(address) && address.AddressFamily == AddressFamily.InterNetworkV6))
        {
            return "127.0.0.1";
        }

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    }

    // Địa chỉ callback tuyệt đối theo host của request hiện tại (spec VNPay §5.1).
    public static string BuildAbsoluteUrl(HttpRequest request, string relativePath)
    {
        var path = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
        return $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
    }
}
