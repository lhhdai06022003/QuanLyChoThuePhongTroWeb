using QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record RoomNoticePageModel(RoomNoticePreview Preview,
    IReadOnlyList<RoomNoticeDelivery>? Results = null,
    string? Message = null);
