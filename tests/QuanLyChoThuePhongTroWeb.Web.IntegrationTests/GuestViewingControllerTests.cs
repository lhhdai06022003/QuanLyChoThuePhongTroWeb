using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;
using QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;
using QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.ViewModels;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

public class GuestViewingControllerTests
{
    [Fact]
    public async Task TaoGet_Returns404ForHiddenRoom()
    {
        var controller = new LichXemController(new FakeRooms(null), new FakeViewings(), new FakeGuests());

        Assert.IsType<NotFoundResult>(await controller.Tao("hidden"));
    }

    [Fact]
    public async Task TaoPost_UsesRouteRoomCodeAndRedirectsAfterSubmission()
    {
        var viewings = new FakeViewings();
        var controller = new LichXemController(new FakeRooms(Room()), viewings, new FakeGuests());
        controller.ControllerContext = new ControllerContext
        { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        var model = new TaoLichXemViewModel
        {
            MaCongKhai = "forged",
            HoTen = "Nguyễn An",
            SoDienThoai = "0900000000",
            ThoiGianMongMuon = DateTime.Today.AddDays(2)
        };

        var result = await controller.Tao("OPEN", model);

        Assert.Equal("OPEN", viewings.LastRequest?.MaCongKhai);
        Assert.Equal(nameof(LichXemController.DaGui), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    private static PublicRoomDto Room() => new("OPEN", "Phòng đẹp", "P1", "Chi nhánh",
        "Địa chỉ", "0900000000", 2500000m, 20, 2, "", Array.Empty<PublicRoomImageDto>());

    private sealed class FakeRooms(PublicRoomDto? room) : IPhongCongKhaiService
    {
        public Task<PublicRoomPageDto> SearchAsync(PublicRoomQuery query) =>
            Task.FromResult(new PublicRoomPageDto(Array.Empty<PublicRoomDto>(), 1, 12, 0));
        public Task<PublicRoomDto?> GetAsync(string code) => Task.FromResult(room);
    }

    private sealed class FakeViewings : ILichXemPhongService
    {
        public Task<IReadOnlyList<ViewingSlotDto>> GetRescheduleSlotsAsync(int requestId, int actorId) =>
            Task.FromResult<IReadOnlyList<ViewingSlotDto>>(Array.Empty<ViewingSlotDto>());
        public CreateViewingRequest? LastRequest { get; private set; }
        public Task<IReadOnlyList<ViewingSlotDto>> GetOpenSlotsAsync(string code) =>
            Task.FromResult<IReadOnlyList<ViewingSlotDto>>(Array.Empty<ViewingSlotDto>());
        public Task<ViewingSubmissionResult> CreateAsync(CreateViewingRequest request, int? actorId = null)
        {
            LastRequest = request;
            return Task.FromResult(new ViewingSubmissionResult(true, "OK"));
        }
        public Task<IReadOnlyList<StaffViewingDto>> GetStaffQueueAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<StaffViewingDto>>(Array.Empty<StaffViewingDto>());
        public Task<IReadOnlyList<GuestViewingDto>> GetMineAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<GuestViewingDto>>(Array.Empty<GuestViewingDto>());
        public Task<bool> DecideAsync(int requestId, int actorId, bool confirm) => Task.FromResult(false);
        public Task<bool> ManageConfirmedAsync(int requestId, int actorId, ManageViewingRequest request) => Task.FromResult(false);
    }

    private sealed class FakeGuests : IKhachVangLaiService
    {
        public Task<GuestProfileDto?> GetProfileAsync(int actorId) => Task.FromResult<GuestProfileDto?>(null);
        public Task<GuestRegistrationResult> RegisterAsync(GuestRegistrationRequest request) =>
            Task.FromResult(new GuestRegistrationResult(false, null, "unused"));
        public Task<GuestRegistrationResult> UpdateIdentityAsync(int actorId, UpdateGuestIdentityRequest request) =>
            Task.FromResult(new GuestRegistrationResult(false, null, "unused"));
    }
}
