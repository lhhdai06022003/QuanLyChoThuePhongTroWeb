using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record ViewingAdminPageModel(IReadOnlyList<ViewingAdminRequestDto> Requests);
