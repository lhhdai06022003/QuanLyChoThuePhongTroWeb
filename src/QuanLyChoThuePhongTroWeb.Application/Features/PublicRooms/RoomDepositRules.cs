namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

// Tiền cọc phòng lưu ở phong_tro."TienCocPhong": 0 = chưa đặt, phòng công khai phải > 0.
public static class RoomDepositRules
{
    public const decimal MaxAmount = 1_000_000_000_000m;

    public static bool IsValidAmount(decimal amount) =>
        amount >= 0 && amount <= MaxAmount && decimal.Truncate(amount) == amount;
}
