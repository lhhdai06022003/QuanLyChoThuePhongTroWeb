using System.ComponentModel.DataAnnotations;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record ViewingAdminPageModel(
    IReadOnlyList<PhongTroListItemDto> Rooms,
    IReadOnlyList<ViewingSlotAdminDto> Slots,
    IReadOnlyList<ViewingAdminRequestDto> Requests);

public sealed class CreateViewingSlotModel
{
    [Range(1, int.MaxValue)]
    public int RoomId { get; set; }

    [Required]
    public DateTime StartLocal { get; set; }

    [Required]
    public DateTime EndLocal { get; set; }

    [Range(1, 100)]
    public int Capacity { get; set; } = 1;
}
