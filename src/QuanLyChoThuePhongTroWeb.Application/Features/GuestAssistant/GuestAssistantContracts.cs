using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GuestAssistant;

public sealed class GuestAssistantRequest : ChatRequest
{
    public int? RoomId { get; set; }
    public List<int> SuggestedRoomIds { get; set; } = [];
}

public sealed record GuestAssistantRoom(int Id, string Number, string Title, string Branch,
    string Address, decimal Rent, double Area, int Occupants, decimal? DepositAmount)
{
    public string DetailUrl => $"/phong/{Id}";
    public string ViewingUrl => $"/phong/{Id}/hen-xem";
    public string? BookingUrl => DepositAmount is > 0 ? $"/phong/{Id}/giu-cho" : null;
}

public sealed record GuestAssistantReply(bool Success, string Message,
    IReadOnlyList<GuestAssistantRoom> Rooms, string? ErrorCode = null)
{
    public static GuestAssistantReply Fail(string message, string code)
        => new(false, message, [], code);
}

public interface IGuestAssistantService
{
    Task<GuestAssistantReply> ChatAsync(GuestAssistantRequest? request,
        IReadOnlyCollection<int>? allowedRoomIds, CancellationToken cancellationToken = default);
}
