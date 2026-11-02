namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed record RoomDepositPolicy(decimal Amount, int PaymentWindowMinutes = 30,
    int ContractWindowHours = 48)
{
    public bool IsValid => Amount > 0 && Amount <= 1_000_000_000_000m &&
        decimal.Truncate(Amount) == Amount && PaymentWindowMinutes is >= 1 and <= 1440 &&
        ContractWindowHours is >= 1 and <= 720;
}

public interface IRoomDepositPolicyStore
{
    Task<RoomDepositPolicy?> GetAsync(int roomId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, RoomDepositPolicy>> GetAllAsync(CancellationToken ct = default);
    Task SetAsync(int roomId, RoomDepositPolicy policy, CancellationToken ct = default);
    Task<bool> UpdateListingAsync(int roomId, RoomDepositPolicy policy,
        Func<CancellationToken, Task<bool>> saveListing, CancellationToken ct = default);
}
