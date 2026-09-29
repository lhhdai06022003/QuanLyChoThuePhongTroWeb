using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

public class PublicRoomControllerTests
{
    [Fact]
    public async Task Index_ReturnsSearchResultsForThePublicRoomView()
    {
        var expected = new PublicRoomPageDto(Array.Empty<PublicRoomDto>(), 1, 12, 0);
        var controller = new PhongController(new FakeService(expected));

        var result = await controller.Index(new PublicRoomQuery());

        Assert.Same(expected, Assert.IsType<ViewResult>(result).Model);
    }

    [Fact]
    public async Task ChiTiet_Returns404WhenRoomIsNotPublic()
    {
        var controller = new PhongController(new FakeService(
            new PublicRoomPageDto(Array.Empty<PublicRoomDto>(), 1, 12, 0)));

        var result = await controller.ChiTiet("hidden");

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class FakeService(PublicRoomPageDto page) : IPhongCongKhaiService
    {
        public Task<PublicRoomPageDto> SearchAsync(PublicRoomQuery query) => Task.FromResult(page);
        public Task<PublicRoomDto?> GetAsync(string code) => Task.FromResult<PublicRoomDto?>(null);
    }
}
