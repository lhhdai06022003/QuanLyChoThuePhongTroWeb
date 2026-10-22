using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class PublicRoomRulesTests
    {
        [Fact]
        public void PhongTro_Defaults_DuocDangTin_To_False()
        {
            var room = new PhongTro();
            Assert.False(room.DuocDangTin);
        }

        [Fact]
        public void PhongTro_ShouldNotHave_NgayDangTin_Or_NgayNgungDang()
        {
            var type = typeof(PhongTro);
            Assert.Null(type.GetProperty("NgayDangTin"));
            Assert.Null(type.GetProperty("NgayNgungDang"));
        }

        [Fact]
        public void PhongTro_HasApprovedPublicProperties()
        {
            var type = typeof(PhongTro);
            Assert.NotNull(type.GetProperty(nameof(PhongTro.DuocDangTin)));
            Assert.NotNull(type.GetProperty(nameof(PhongTro.TieuDeDangTin)));
            Assert.NotNull(type.GetProperty(nameof(PhongTro.MaCongKhai)));
            Assert.NotNull(type.GetProperty(nameof(PhongTro.NguoiDangTinId)));
        }

        [Theory]
        [InlineData(false, TrangThaiPhong.Trong, false, false)]
        [InlineData(true, TrangThaiPhong.DaThue, false, false)]
        [InlineData(true, TrangThaiPhong.BaoTri, false, false)]
        [InlineData(true, TrangThaiPhong.Trong, true, false)]
        [InlineData(true, TrangThaiPhong.Trong, false, true)]
        public void PhongTro_IsPubliclyVisible_EvaluatesCorrectly(
            bool duocDangTin,
            TrangThaiPhong trangThai,
            bool isDeleted,
            bool expectedVisible)
        {
            var room = new PhongTro
            {
                DuocDangTin = duocDangTin,
                TrangThai = trangThai,
                IsDeleted = isDeleted
            };

            var isVisible = room.DuocDangTin &&
                            room.TrangThai == TrangThaiPhong.Trong &&
                            !room.IsDeleted;

            Assert.Equal(expectedVisible, isVisible);
        }

        [Fact]
        public void AnhPhongTro_CanTrackGalleryMetadata()
        {
            var photo = new AnhPhongTro
            {
                AnhPhongTroId = 1,
                PhongTroId = 10,
                Url = "https://res.cloudinary.com/demo/image/upload/sample.jpg",
                PublicId = "rooms/sample",
                ThuTuHienThi = 1,
                LaAnhDaiDien = true,
                ChuThich = "Phòng khách",
                IsActive = true,
                NguoiTaiLenId = 5,
                NgayTaiLen = DateTime.UtcNow
            };

            Assert.Equal(10, photo.PhongTroId);
            Assert.True(photo.LaAnhDaiDien);
            Assert.True(photo.IsActive);
        }

        [Fact]
        public void PhongTro_OnlyOneActiveCoverImageAllowed()
        {
            var photos = new List<AnhPhongTro>
            {
                new() { AnhPhongTroId = 1, PhongTroId = 1, LaAnhDaiDien = true, IsActive = true },
                new() { AnhPhongTroId = 2, PhongTroId = 1, LaAnhDaiDien = false, IsActive = true },
                new() { AnhPhongTroId = 3, PhongTroId = 1, LaAnhDaiDien = true, IsActive = false }
            };

            var activeCoverCount = photos.Count(p => p.LaAnhDaiDien && p.IsActive);
            Assert.Equal(1, activeCoverCount);
        }
    }
}
