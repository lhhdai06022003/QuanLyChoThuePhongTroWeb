using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

public sealed class ViewingPageModel
{
    [ValidateNever]
    public PublicRoomCardModel Room { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng chọn ngày giờ muốn xem phòng.")]
    public DateTime? PreferredTimeLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    public string? Note { get; set; }
}
