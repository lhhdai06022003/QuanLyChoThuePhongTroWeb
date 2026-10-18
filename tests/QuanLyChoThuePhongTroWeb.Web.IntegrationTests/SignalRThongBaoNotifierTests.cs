using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Hubs;
using QuanLyChoThuePhongTroWeb.Web.Hubs;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Không cần DB hay host: hub giả ghi lại người nhận, tên sự kiện và nội dung (spec thanh toán trực tuyến §3.3).
    public class SignalRThongBaoNotifierTests
    {
        private sealed class FakeClientProxy : IClientProxy
        {
            public List<(string Method, object?[] Args, CancellationToken Token)> Calls { get; } = new();

            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                Calls.Add((method, args, cancellationToken));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeHubClients : IHubClients
        {
            public Dictionary<string, FakeClientProxy> UserProxies { get; } = new();
            public Dictionary<string, FakeClientProxy> GroupProxies { get; } = new();

            public IClientProxy User(string userId)
            {
                if (!UserProxies.TryGetValue(userId, out var proxy))
                {
                    proxy = UserProxies[userId] = new FakeClientProxy();
                }

                return proxy;
            }

            public IClientProxy Group(string groupName)
            {
                if (!GroupProxies.TryGetValue(groupName, out var proxy))
                {
                    proxy = GroupProxies[groupName] = new FakeClientProxy();
                }

                return proxy;
            }

            public IClientProxy All => throw new NotSupportedException();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Client(string connectionId) => throw new NotSupportedException();
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
            public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
        }

        private sealed class FakeHubContext : IHubContext<ThongBaoHub>
        {
            public FakeHubClients FakeClients { get; } = new();
            public IHubClients Clients => FakeClients;
            public IGroupManager Groups => throw new NotSupportedException();
        }

        private readonly FakeHubContext _hub = new();

        private SignalRThongBaoNotifier CreateNotifier() => new(_hub);

        [Fact]
        public async Task InvoicePaymentUpdated_IsSentToTheUser_AsHoaDonThanhToanCapNhat()
        {
            await CreateNotifier().SendInvoicePaymentUpdatedAsync(500, new InvoicePaymentUpdatedEvent(7, 0m, "Đã thanh toán"));

            var proxy = Assert.Single(_hub.FakeClients.UserProxies);
            Assert.Equal("500", proxy.Key);
            var call = Assert.Single(proxy.Value.Calls);
            Assert.Equal("HoaDonThanhToanCapNhat", call.Method);
            var payload = Assert.Single(call.Args)!;
            var json = JsonSerializer.SerializeToElement(payload);
            Assert.Equal(7, json.GetProperty("hoaDonId").GetInt32());
            Assert.Equal(0m, json.GetProperty("conLai").GetDecimal());
            Assert.Equal("Đã thanh toán", json.GetProperty("trangThai").GetString());
            Assert.Empty(_hub.FakeClients.GroupProxies);
        }

        [Fact]
        public async Task InvoicePaymentUpdated_CarriesTheAmountStillOwed()
        {
            await CreateNotifier().SendInvoicePaymentUpdatedAsync(501, new InvoicePaymentUpdatedEvent(8, 1_500_000m, "Thanh toán một phần"));

            var call = Assert.Single(Assert.Single(_hub.FakeClients.UserProxies, p => p.Key == "501").Value.Calls);
            var json = JsonSerializer.SerializeToElement(Assert.Single(call.Args)!);
            Assert.Equal(8, json.GetProperty("hoaDonId").GetInt32());
            Assert.Equal(1_500_000m, json.GetProperty("conLai").GetDecimal());
            Assert.Equal("Thanh toán một phần", json.GetProperty("trangThai").GetString());
        }

        [Fact]
        public async Task InvoicePaymentUpdated_ForwardsTheCancellationToken()
        {
            using var cts = new CancellationTokenSource();

            await CreateNotifier().SendInvoicePaymentUpdatedAsync(500, new InvoicePaymentUpdatedEvent(7, 0m, "Đã thanh toán"), cts.Token);

            Assert.Equal(cts.Token, Assert.Single(_hub.FakeClients.UserProxies["500"].Calls).Token);
        }

        [Fact]
        public async Task InvoicePaymentUpdated_IsADifferentEventFromTheOrdinaryNotification()
        {
            var notifier = CreateNotifier();

            await notifier.SendToUserAsync(500, new { tieuDe = "x" });
            await notifier.SendInvoicePaymentUpdatedAsync(500, new InvoicePaymentUpdatedEvent(7, 0m, "Đã thanh toán"));

            var methods = _hub.FakeClients.UserProxies["500"].Calls.ConvertAll(c => c.Method);
            Assert.Equal(new[] { "NhanThongBao", "HoaDonThanhToanCapNhat" }, methods);
        }

        [Fact]
        public async Task TheNotifier_ImplementsTheInterfaceMethod_NotTheNoOpDefault()
        {
            IThongBaoNotifier notifier = CreateNotifier();

            await notifier.SendInvoicePaymentUpdatedAsync(500, new InvoicePaymentUpdatedEvent(7, 0m, "Đã thanh toán"));

            Assert.Single(_hub.FakeClients.UserProxies["500"].Calls);
        }
    }
}
