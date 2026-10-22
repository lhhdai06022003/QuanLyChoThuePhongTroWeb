using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ViewingStatusEmailTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 7, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ViewingAdminAction.Confirm, "DaXacNhanLich")]
    [InlineData(ViewingAdminAction.Reject, "TuChoi")]
    [InlineData(ViewingAdminAction.CancelBecauseRented, "DaHuy")]
    public async Task SendsRequestedContactEmailOnlyAfterSuccessfulUpdate(ViewingAdminAction action, string status)
    {
        var store = new Store();
        var mail = new Mail(store);
        var result = await Service(store, mail).UpdateAsync(3, 10, action, Now.AddDays(1), "Khách đã được thông báo");
        Assert.True(result.Success, result.Message);
        Assert.True(store.Persisted);
        var sent = Assert.Single(mail.Calls);
        Assert.Equal("viewing-contact@example.com", sent.Email);
        Assert.Equal(status, sent.Status);
        if (action == ViewingAdminAction.Confirm)
            Assert.Equal(Now.AddDays(1), sent.ConfirmedTimeUtc);
    }

    [Theory]
    [InlineData(ViewingAdminError.InvalidState)]
    [InlineData(ViewingAdminError.Conflict)]
    [InlineData(ViewingAdminError.NotFound)]
    public async Task FailedTransitionDoesNotSendEmail(ViewingAdminError error)
    {
        var store = new Store { Error = error };
        var mail = new Mail(store);
        var result = await Service(store, mail).UpdateAsync(3, 10, ViewingAdminAction.Reject, null, "Không phù hợp");
        Assert.False(result.Success);
        Assert.Empty(mail.Calls);
    }

    [Fact]
    public async Task EmployeeOutsideBranchCannotUpdateOrSend()
    {
        var store = new Store();
        var mail = new Mail(store);
        var result = await Service(store, mail, false).UpdateAsync(3, 10, ViewingAdminAction.Reject, null, "Không phù hợp");
        Assert.False(result.Success);
        Assert.Equal(0, store.Writes);
        Assert.Empty(mail.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SmtpFailureKeepsCommittedStateAndReturnsWarning(bool throws)
    {
        var store = new Store();
        var mail = new Mail(store) { Fails = true, Throws = throws };
        var result = await Service(store, mail).UpdateAsync(3, 10, ViewingAdminAction.Confirm, Now.AddDays(1), null);
        Assert.True(result.Success);
        Assert.True(store.Persisted);
        Assert.Contains("chưa gửi", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingRecipientDoesNotUndoUpdate()
    {
        var store = new Store { Notice = Store.DefaultNotice with { Email = "" } };
        var mail = new Mail(store);
        var result = await Service(store, mail).UpdateAsync(3, 10, ViewingAdminAction.Reject, null, "Không phù hợp");
        Assert.True(result.Success);
        Assert.True(store.Persisted);
        Assert.Empty(mail.Calls);
        Assert.Contains("email", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkViewedDoesNotSendAnotherStatusEmail()
    {
        var store = new Store();
        var mail = new Mail(store);
        Assert.True((await Service(store, mail).UpdateAsync(3, 10, ViewingAdminAction.MarkViewed, null, null)).Success);
        Assert.Empty(mail.Calls);
    }

    [Fact]
    public async Task ResendChecksBranchAndDoesNotRepeatStateTransition()
    {
        var store = new Store { Persisted = true, Notice = Store.DefaultNotice with { Status = "DaHuy" } };
        var mail = new Mail(store);
        var method = typeof(ViewingAdminRequestService).GetMethod("ResendNotificationAsync");
        Assert.NotNull(method);
        var denied = await (Task<ServiceResult>)method.Invoke(Service(store, mail, false), [3, 10, CancellationToken.None])!;
        Assert.False(denied.Success);
        Assert.Empty(mail.Calls);
        var allowed = await (Task<ServiceResult>)method.Invoke(Service(store, mail), [3, 10, CancellationToken.None])!;
        Assert.True(allowed.Success);
        Assert.Single(mail.Calls);
        Assert.Equal(0, store.Writes);
    }

    private static ViewingAdminRequestService Service(Store store, Mail mail, bool canAccess = true)
    {
        var access = new FakeEmployeeAccessService { ScopeToReturn = new EmployeeAccessScope(3, false, canAccess ? [42] : [99]) };
        var ctor = typeof(ViewingAdminRequestService).GetConstructors().Single();
        var args = ctor.GetParameters().Select(p =>
            p.ParameterType == typeof(IViewingAdminRequestStore) ? (object)store :
            p.ParameterType == typeof(IEmployeeAccessService) ? access :
            p.ParameterType == typeof(TimeProvider) ? new FixedTimeProvider(Now) :
            p.ParameterType == typeof(IEmailService) ? mail :
            p.ParameterType == typeof(ILogger<ViewingAdminRequestService>) ? NullLogger<ViewingAdminRequestService>.Instance :
            throw new InvalidOperationException("Unexpected constructor dependency")).ToArray();
        return (ViewingAdminRequestService)ctor.Invoke(args);
    }

    private sealed class Store : IViewingAdminRequestStore
    {
        public static readonly ViewingAdminRequestDto DefaultNotice = new(10, 7, 42, "107", "Khách xem phòng",
            "0912345678", "viewing-contact@example.com", Now.AddDays(1), null, "ChoNhanVienXuLy", null, "Gò Vấp");
        public ViewingAdminRequestDto Notice = DefaultNotice;
        public ViewingAdminError Error;
        public bool Persisted;
        public int Writes;
        public Task<IReadOnlyList<ViewingAdminRequestDto>> ListAsync(IReadOnlyCollection<int>? branches, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ViewingAdminRequestDto>>([Notice]);
        public Task<int?> GetBranchIdAsync(int id, CancellationToken ct = default) => Task.FromResult<int?>(42);
        public Task<ViewingAdminRequestDto?> GetNotificationAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<ViewingAdminRequestDto?>(Notice);
        public Task<ViewingAdminError> UpdateAsync(int actorId, int id, ViewingAdminAction action,
            DateTime? confirmed, string? reason, DateTime nowUtc, CancellationToken ct = default)
        {
            Writes++;
            if (Error == ViewingAdminError.None)
            {
                Persisted = true;
                Notice = Notice with
                {
                    Status = action switch { ViewingAdminAction.Confirm => "DaXacNhanLich",
                        ViewingAdminAction.Reject => "TuChoi", ViewingAdminAction.CancelBecauseRented => "DaHuy", _ => "DaXemPhong" },
                    ConfirmedTimeUtc = confirmed
                };
            }
            return Task.FromResult(Error);
        }
    }

    private sealed class Mail(Store store) : IEmailService
    {
        public List<ViewingAdminRequestDto> Calls = [];
        public bool Fails, Throws;
        public Task<(bool IsSuccess, string ErrorMessage)> SendViewingStatusNoticeAsync(ViewingAdminRequestDto notice)
        {
            Assert.True(store.Persisted, "Email attempted before successful state transition.");
            Calls.Add(notice);
            if (Throws) throw new InvalidOperationException("SMTP unavailable");
            return Task.FromResult((!Fails, Fails ? "SMTP unavailable" : ""));
        }
        public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string to, HoaDonChiTietRes invoice, byte[] pdf) => Task.FromResult((true, ""));
        public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string to, string name, ContractExpiryAlertData data) => Task.FromResult((true, ""));
    }
}
