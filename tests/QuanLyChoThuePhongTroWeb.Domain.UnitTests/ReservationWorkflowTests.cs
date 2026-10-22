using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class ReservationWorkflowTests
    {
        [Fact]
        public void YeuCauGiuCho_Requires_KhachVangLai_And_AllowsNullable_Viewing()
        {
            var reservation = new YeuCauGiuCho
            {
                YeuCauGiuChoId = 1,
                KhachVangLaiId = 10,
                PhongTroId = 5,
                YeuCauXemPhongId = null,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                NgayTao = DateTime.UtcNow
            };

            Assert.Equal(10, reservation.KhachVangLaiId);
            Assert.Null(reservation.YeuCauXemPhongId);
            Assert.Equal(TrangThaiYeuCauGiuCho.MoiTao, reservation.TrangThai);
        }

        [Fact]
        public void StaffApproval_SetsAmount_PaymentDeadline_And_TransitionsTo_ChoThanhToan()
        {
            var reservation = new YeuCauGiuCho
            {
                YeuCauGiuChoId = 2,
                KhachVangLaiId = 10,
                PhongTroId = 5,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao
            };

            // Employee approval
            var approvedAmount = 1000000m;
            var paymentDeadline = DateTime.UtcNow.AddHours(24);
            var contractDeadline = DateTime.UtcNow.AddDays(7);
            var staffId = 99;

            reservation.SoTienGiuCho = approvedAmount;
            reservation.HanThanhToan = paymentDeadline;
            reservation.HanKyHopDong = contractDeadline;
            reservation.NguoiDuyetId = staffId;
            reservation.NgayDuyet = DateTime.UtcNow;
            reservation.TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan;

            Assert.Equal(1000000m, reservation.SoTienGiuCho);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, reservation.TrangThai);
            Assert.NotNull(reservation.HanThanhToan);
            Assert.Equal(staffId, reservation.NguoiDuyetId);
        }

        [Theory]
        [InlineData(TrangThaiYeuCauGiuCho.ChoThanhToan, true)]
        [InlineData(TrangThaiYeuCauGiuCho.ChoXacNhanTien, true)]
        [InlineData(TrangThaiYeuCauGiuCho.DangGiuCho, true)]
        [InlineData(TrangThaiYeuCauGiuCho.MoiTao, false)]
        [InlineData(TrangThaiYeuCauGiuCho.DaChuyenHopDong, false)]
        [InlineData(TrangThaiYeuCauGiuCho.TuChoi, false)]
        [InlineData(TrangThaiYeuCauGiuCho.HetHan, false)]
        [InlineData(TrangThaiYeuCauGiuCho.DaHuy, false)]
        public void ActiveReservation_States_LockingRoom_IdentifiedAccurately(
            TrangThaiYeuCauGiuCho trangThai,
            bool expectedActiveLock)
        {
            var isActiveLock = trangThai is TrangThaiYeuCauGiuCho.ChoThanhToan
                                       or TrangThaiYeuCauGiuCho.ChoXacNhanTien
                                       or TrangThaiYeuCauGiuCho.DangGiuCho;

            Assert.Equal(expectedActiveLock, isActiveLock);
        }

        [Fact]
        public void Room_PublicVisibility_ConsidersActiveReservations()
        {
            var room = new PhongTro
            {
                DuocDangTin = true,
                TrangThai = TrangThaiPhong.Trong,
                IsDeleted = false
            };

            var reservations = new List<YeuCauGiuCho>
            {
                new() { PhongTroId = room.PhongTroId, TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan }
            };

            var hasActiveReservation = reservations.Any(r =>
                r.TrangThai is TrangThaiYeuCauGiuCho.ChoThanhToan
                            or TrangThaiYeuCauGiuCho.ChoXacNhanTien
                            or TrangThaiYeuCauGiuCho.DangGiuCho);

            var isPubliclyVisible = room.DuocDangTin &&
                                   room.TrangThai == TrangThaiPhong.Trong &&
                                   !room.IsDeleted &&
                                   !hasActiveReservation;

            Assert.False(isPubliclyVisible); // Locked by active reservation!

            // When reservation expires or cancelled:
            reservations[0].TrangThai = TrangThaiYeuCauGiuCho.HetHan;
            hasActiveReservation = reservations.Any(r =>
                r.TrangThai is TrangThaiYeuCauGiuCho.ChoThanhToan
                            or TrangThaiYeuCauGiuCho.ChoXacNhanTien
                            or TrangThaiYeuCauGiuCho.DangGiuCho);

            isPubliclyVisible = room.DuocDangTin &&
                               room.TrangThai == TrangThaiPhong.Trong &&
                               !room.IsDeleted &&
                               !hasActiveReservation;

            Assert.True(isPubliclyVisible); // Automatically reappears without re-enabling DuocDangTin
        }

        [Fact]
        public void LichSuTrangThaiYeuCauGiuCho_TracksTransitions()
        {
            var history = new LichSuTrangThaiYeuCauGiuCho
            {
                LichSuTrangThaiYeuCauGiuChoId = 1,
                YeuCauGiuChoId = 2,
                TrangThaiTruoc = TrangThaiYeuCauGiuCho.MoiTao,
                TrangThaiSau = TrangThaiYeuCauGiuCho.ChoThanhToan,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = 99,
                NgayThucHien = DateTime.UtcNow,
                LyDo = "Nhân viên duyệt yêu cầu và yêu cầu chuyển tiền"
            };

            Assert.Equal(TrangThaiYeuCauGiuCho.MoiTao, history.TrangThaiTruoc);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, history.TrangThaiSau);
            Assert.Equal(99, history.NguoiThucHienId);
        }
    }
}
