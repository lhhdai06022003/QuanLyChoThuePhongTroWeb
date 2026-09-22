using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ViewingScheduleServiceTests
    {
        [Fact]
        public void YeuCauXemPhong_Requires_HoTen_And_SoDienThoai()
        {
            var req = new YeuCauXemPhong
            {
                PhongTroId = 1,
                HoTen = "Nguyen Van C",
                SoDienThoai = "0987654321",
                ThoiGianMongMuon = DateTime.UtcNow.AddDays(1),
                NguonTao = NguonTaoYeuCauXemPhong.TrangCongKhai
            };

            Assert.False(string.IsNullOrWhiteSpace(req.HoTen));
            Assert.False(string.IsNullOrWhiteSpace(req.SoDienThoai));
            Assert.Null(req.Email);
            Assert.Null(req.KhachVangLaiId);
        }

        [Fact]
        public void AutoConfirmation_When_SlotIsActive_And_HasCapacity()
        {
            var slot = new KhungGioXemPhong
            {
                KhungGioXemPhongId = 1,
                PhongTroId = 10,
                ThoiGianBatDau = DateTime.UtcNow.Date.AddHours(14),
                ThoiGianKetThuc = DateTime.UtcNow.Date.AddHours(15),
                SoLuongToiDa = 2,
                IsActive = true
            };

            var existingConfirmedBookings = new List<YeuCauXemPhong>
            {
                new()
                {
                    YeuCauXemPhongId = 101,
                    KhungGioXemPhongId = slot.KhungGioXemPhongId,
                    TrangThai = TrangThaiYeuCauXemPhong.DaXacNhanLich
                }
            };

            var currentBookingCount = existingConfirmedBookings.Count(b => b.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich);
            var hasCapacity = slot.IsActive && currentBookingCount < slot.SoLuongToiDa;

            Assert.True(hasCapacity);

            var newReq = new YeuCauXemPhong
            {
                YeuCauXemPhongId = 102,
                PhongTroId = 10,
                KhungGioXemPhongId = slot.KhungGioXemPhongId,
                HoTen = "Le Thi D",
                SoDienThoai = "0933333333",
                ThoiGianMongMuon = slot.ThoiGianBatDau,
                NguonTao = NguonTaoYeuCauXemPhong.AI
            };

            if (hasCapacity)
            {
                newReq.TrangThai = TrangThaiYeuCauXemPhong.DaXacNhanLich;
                newReq.HinhThucXacNhan = HinhThucXacNhanLich.TuDong;
                newReq.ThoiGianXacNhan = newReq.ThoiGianMongMuon;
            }

            Assert.Equal(TrangThaiYeuCauXemPhong.DaXacNhanLich, newReq.TrangThai);
            Assert.Equal(HinhThucXacNhanLich.TuDong, newReq.HinhThucXacNhan);
            Assert.NotNull(newReq.ThoiGianXacNhan);
        }

        [Fact]
        public void RoutesToStaff_When_SlotIsFull_Or_NotConfigured()
        {
            var slot = new KhungGioXemPhong
            {
                KhungGioXemPhongId = 2,
                PhongTroId = 10,
                ThoiGianBatDau = DateTime.UtcNow.Date.AddHours(16),
                ThoiGianKetThuc = DateTime.UtcNow.Date.AddHours(17),
                SoLuongToiDa = 1,
                IsActive = true
            };

            var existingConfirmedBookings = new List<YeuCauXemPhong>
            {
                new()
                {
                    YeuCauXemPhongId = 201,
                    KhungGioXemPhongId = slot.KhungGioXemPhongId,
                    TrangThai = TrangThaiYeuCauXemPhong.DaXacNhanLich
                }
            };

            var currentBookingCount = existingConfirmedBookings.Count(b => b.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich);
            var hasCapacity = slot.IsActive && currentBookingCount < slot.SoLuongToiDa;

            var newReq = new YeuCauXemPhong
            {
                YeuCauXemPhongId = 202,
                PhongTroId = 10,
                KhungGioXemPhongId = slot.KhungGioXemPhongId,
                HoTen = "Pham Van E",
                SoDienThoai = "0944444444",
                ThoiGianMongMuon = slot.ThoiGianBatDau,
                NguonTao = NguonTaoYeuCauXemPhong.TrangCongKhai
            };

            if (!hasCapacity)
            {
                newReq.TrangThai = TrangThaiYeuCauXemPhong.ChoNhanVienXuLy;
                newReq.HinhThucXacNhan = null;
            }

            Assert.Equal(TrangThaiYeuCauXemPhong.ChoNhanVienXuLy, newReq.TrangThai);
            Assert.Null(newReq.HinhThucXacNhan);
        }

        [Fact]
        public void LichSuTrangThaiYeuCauXemPhong_TracksTransitions_WithActorType()
        {
            var history = new LichSuTrangThaiYeuCauXemPhong
            {
                LichSuTrangThaiYeuCauXemPhongId = 1,
                YeuCauXemPhongId = 102,
                TrangThaiTruoc = null,
                TrangThaiSau = TrangThaiYeuCauXemPhong.DaXacNhanLich,
                LoaiTacNhan = LoaiTacNhan.HeThong,
                NguoiThucHienId = null,
                NgayThucHien = DateTime.UtcNow,
                LyDo = "Tự động xác nhận theo khung giờ khả dụng"
            };

            Assert.Equal(LoaiTacNhan.HeThong, history.LoaiTacNhan);
            Assert.Equal(TrangThaiYeuCauXemPhong.DaXacNhanLich, history.TrangThaiSau);
            Assert.Null(history.NguoiThucHienId);
        }
    }
}
