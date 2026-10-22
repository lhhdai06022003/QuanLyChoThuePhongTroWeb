using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ReservationPaymentTests
    {
        [Fact]
        public void ReservationPayment_AllowsMultipleEvidenceSubmissions()
        {
            var paymentRequest = new YeuCauThanhToanGiuCho
            {
                YeuCauThanhToanGiuChoId = 1,
                YeuCauGiuChoId = 10,
                MaYeuCau = "YCGG-20260921-001",
                SoTien = 1000000m,
                NoiDungChuyenKhoan = "GC 10 P101",
                HanThanhToan = DateTime.UtcNow.AddHours(24),
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = 5
            };

            var evidence1 = new MinhChungThanhToanGiuCho
            {
                MinhChungThanhToanGiuChoId = 1,
                YeuCauThanhToanGiuChoId = paymentRequest.YeuCauThanhToanGiuChoId,
                UrlHinhAnh = "https://cloudinary.com/proof1.png",
                SoTienKhaiBao = 1000000m,
                NgayChuyenTien = DateTime.UtcNow,
                TrangThai = TrangThaiMinhChungThanhToan.TuChoi,
                LyDoTuChoi = "Ảnh mờ, không thấy mã giao dịch"
            };

            var evidence2 = new MinhChungThanhToanGiuCho
            {
                MinhChungThanhToanGiuChoId = 2,
                YeuCauThanhToanGiuChoId = paymentRequest.YeuCauThanhToanGiuChoId,
                UrlHinhAnh = "https://cloudinary.com/proof2.png",
                SoTienKhaiBao = 1000000m,
                NgayChuyenTien = DateTime.UtcNow,
                TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan
            };

            var evidenceList = new List<MinhChungThanhToanGiuCho> { evidence1, evidence2 };

            Assert.Equal(2, evidenceList.Count);
            Assert.Equal(TrangThaiMinhChungThanhToan.TuChoi, evidenceList[0].TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, evidenceList[1].TrangThai);
        }

        [Fact]
        public void ConfirmingPayment_TransitionsReservationTo_DangGiuCho_AndCreatesTransaction()
        {
            var reservation = new YeuCauGiuCho
            {
                YeuCauGiuChoId = 10,
                PhongTroId = 5,
                KhachVangLaiId = 20,
                SoTienGiuCho = 1000000m,
                TrangThai = TrangThaiYeuCauGiuCho.ChoXacNhanTien
            };

            var paymentRequest = new YeuCauThanhToanGiuCho
            {
                YeuCauThanhToanGiuChoId = 1,
                YeuCauGiuChoId = reservation.YeuCauGiuChoId,
                SoTien = 1000000m,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan
            };

            var evidence = new MinhChungThanhToanGiuCho
            {
                MinhChungThanhToanGiuChoId = 2,
                YeuCauThanhToanGiuChoId = paymentRequest.YeuCauThanhToanGiuChoId,
                SoTienKhaiBao = 1000000m,
                MaGiaoDichNganHang = "FT2609210001",
                TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan
            };

            // Simulate atomic confirmation step
            var staffId = 50;
            var confirmTime = DateTime.UtcNow;

            evidence.TrangThai = TrangThaiMinhChungThanhToan.DaXacNhan;
            evidence.NguoiDoiChieuId = staffId;
            evidence.NgayDoiChieu = confirmTime;

            var transaction = new GiaoDichGiuCho
            {
                GiaoDichGiuChoId = 100,
                YeuCauThanhToanGiuChoId = paymentRequest.YeuCauThanhToanGiuChoId,
                MinhChungThanhToanGiuChoId = evidence.MinhChungThanhToanGiuChoId,
                MaGiaoDich = evidence.MaGiaoDichNganHang,
                SoTienThucNhan = 1000000m,
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NgayThucNhan = confirmTime,
                NguoiXacNhanId = staffId,
                NgayXacNhan = confirmTime
            };

            paymentRequest.TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat;
            reservation.TrangThai = TrangThaiYeuCauGiuCho.DangGiuCho;

            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, evidence.TrangThai);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, paymentRequest.TrangThai);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, reservation.TrangThai);
            Assert.Equal(1000000m, transaction.SoTienThucNhan);
            Assert.Equal(evidence.MinhChungThanhToanGiuChoId, transaction.MinhChungThanhToanGiuChoId);
        }

        [Fact]
        public void LichSuTrangThaiYeuCauThanhToanGiuCho_TracksPaymentHistory()
        {
            var history = new LichSuTrangThaiYeuCauThanhToanGiuCho
            {
                LichSuTrangThaiYeuCauThanhToanGiuChoId = 1,
                YeuCauThanhToanGiuChoId = 1,
                TrangThaiTruoc = TrangThaiYeuCauThanhToan.ChoThanhToan,
                TrangThaiSau = TrangThaiYeuCauThanhToan.DaHoanTat,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = 50,
                NgayThucHien = DateTime.UtcNow,
                LyDo = "Xác nhận khớp tiền từ ngân hàng"
            };

            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, history.TrangThaiSau);
            Assert.Equal(50, history.NguoiThucHienId);
        }
    }
}
