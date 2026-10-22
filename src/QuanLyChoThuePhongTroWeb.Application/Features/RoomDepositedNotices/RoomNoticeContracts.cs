namespace QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;

public sealed record RoomNoticeRecipient(string Name, string Email,
    string Source);

public sealed record RoomNoticePreview(int RoomId, int BranchId,
    string RoomNumber, string BranchName,
    IReadOnlyList<RoomNoticeRecipient> Recipients, string RecipientDigest);

public sealed record RoomNoticeDelivery(string Email, bool Sent,
    string? Error);
