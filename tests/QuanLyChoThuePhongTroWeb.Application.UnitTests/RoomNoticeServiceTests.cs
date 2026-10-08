using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class RoomNoticeServiceTests
{
    [Fact]
    public async Task Send_RejectsChangedRecipientList_WithoutSending()
    {
        var email = new FakeEmail();
        var service = CreateService(email);

        var result = await service.SendAsync(3, 7, "old-list");

        Assert.False(result.Success);
        Assert.Empty(email.SentTo);
    }

    [Fact]
    public async Task Send_EmailsEachRecipientSeparately()
    {
        var email = new FakeEmail();
        var service = CreateService(email);

        var result = await service.SendAsync(3, 7, "current-list");

        Assert.True(result.Success);
        Assert.Equal(["a@example.com", "b@example.com"], email.SentTo);
    }

    private static RoomNoticeService CreateService(FakeEmail email) => new(
        new FakeStore(), new FakeEmployeeAccessService
        {
            ScopeToReturn = new EmployeeAccessScope(3, false, [4])
        }, email, new FixedTimeProvider(new DateTime(2026, 10, 7, 8, 0, 0,
            DateTimeKind.Utc)));

    private sealed class FakeStore : IRoomNoticeStore
    {
        public Task<RoomNoticePreview?> GetPreviewAsync(int roomId, DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomNoticePreview?>(new RoomNoticePreview(roomId, 4,
                "A1", "Chi nhánh A", [
                    new RoomNoticeRecipient("A", "a@example.com", "Lịch xem"),
                    new RoomNoticeRecipient("B", "b@example.com", "Giữ chỗ")
                ], "current-list"));
    }

    private sealed class FakeEmail : IEmailService
    {
        public List<string> SentTo { get; } = [];
        public Task<(bool IsSuccess, string ErrorMessage)> SendRoomDepositedNoticeAsync(
            string toEmail, string recipientName, string roomNumber,
            string branchName)
        {
            SentTo.Add(toEmail);
            return Task.FromResult((true, string.Empty));
        }
        public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(
            string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes) =>
            Task.FromResult((true, string.Empty));
        public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(
            string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData) =>
            Task.FromResult((true, string.Empty));
    }
}
