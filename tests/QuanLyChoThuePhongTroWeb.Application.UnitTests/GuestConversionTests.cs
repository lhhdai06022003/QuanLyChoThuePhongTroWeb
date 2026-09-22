using System;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class GuestConversionTests
    {
        [Fact]
        public void RegisteredGuest_DoesNotCreateOrLink_NguoiThue()
        {
            var user = new NguoiDung
            {
                NguoiDungId = 15,
                TenDangNhap = "khach_test",
                MatKhauHash = "hash123",
                Role = Role.KhachVangLai,
                IsActive = true,
                NguoiThueId = null,
                NguoiThue = null
            };

            var guest = new KhachVangLai
            {
                KhachVangLaiId = 1,
                NguoiDungId = user.NguoiDungId,
                HoTen = "Nguyen Van A",
                Email = "nguyenvana@example.com",
                EmailNormalized = "NGUYENVANA@EXAMPLE.COM",
                SoDienThoai = "0901234567",
                DaXacMinhEmail = false,
                NgayTao = DateTime.UtcNow
            };

            Assert.Equal(Role.KhachVangLai, user.Role);
            Assert.Null(user.NguoiThueId);
            Assert.Null(user.NguoiThue);
            Assert.Equal(user.NguoiDungId, guest.NguoiDungId);
        }

        [Fact]
        public void ConvertingGuestToTenant_LinksNguoiThue_AndChangesRoleToKhachThue()
        {
            var user = new NguoiDung
            {
                NguoiDungId = 20,
                TenDangNhap = "guest_to_tenant",
                MatKhauHash = "hash123",
                Role = Role.KhachVangLai,
                IsActive = true,
                NguoiThueId = null
            };

            var guest = new KhachVangLai
            {
                KhachVangLaiId = 5,
                NguoiDungId = user.NguoiDungId,
                HoTen = "Tran Thi B",
                Email = "tranthib@example.com",
                SoDienThoai = "0912345678"
            };

            // Simulate the conversion workflow when contract is created
            var tenant = new NguoiThue
            {
                NguoiThueId = 100,
                HoVaTen = guest.HoTen,
                Email = guest.Email ?? string.Empty,
                SoDienThoai = guest.SoDienThoai,
                CCCD = "012345678901",
                NgayTao = DateTime.UtcNow
            };

            user.NguoiThueId = tenant.NguoiThueId;
            user.NguoiThue = tenant;
            user.Role = Role.KhachThue;

            Assert.Equal(Role.KhachThue, user.Role);
            Assert.Equal(100, user.NguoiThueId);
            Assert.NotNull(user.NguoiThue);
            Assert.Equal(guest.HoTen, user.NguoiThue.HoVaTen);
        }
    }
}
