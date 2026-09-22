using System;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class InvoiceWorkflowTests
    {
        [Fact]
        public void HoaDon_DefaultValues_AreCorrect()
        {
            var hoaDon = new HoaDon { TongTien = 1_000_000m };

            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, hoaDon.TrangThaiPhatHanh);
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.False(hoaDon.ChoPhepThanhToanMotPhan);
            Assert.Null(hoaDon.SoTienThanhToanToiThieu);
        }

        [Fact]
        public void HoaDon_ChotHoaDon_SetsDaChotAndRecordsActorAndTimestamp()
        {
            var hoaDon = new HoaDon
            {
                TongTien = 2_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap
            };

            hoaDon.ChotHoaDon(nguoiChotId: 5, lyDo: "Chốt tiền phòng tháng 9");

            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, hoaDon.TrangThaiPhatHanh);
            Assert.Equal(5, hoaDon.NguoiChotId);
            Assert.NotNull(hoaDon.NgayChot);
            Assert.Single(hoaDon.LichSuTrangThaiHoaDons);

            var history = hoaDon.LichSuTrangThaiHoaDons.GetEnumerator();
            history.MoveNext();
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, history.Current.TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, history.Current.TrangThaiPhatHanhMoi);
            Assert.Equal("Chốt tiền phòng tháng 9", history.Current.LyDo);
        }

        [Fact]
        public void HoaDon_CannotRequestPayment_IfNotDaChotOrDaGui()
        {
            var hoaDonNhap = new HoaDon
            {
                TongTien = 1_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap
            };

            var ex = Assert.Throws<InvalidOperationException>(() => hoaDonNhap.KiemTraDuDieuKienYeuCauThanhToan());
            Assert.Contains("đã chốt", ex.Message, StringComparison.OrdinalIgnoreCase);

            hoaDonNhap.ChotHoaDon(nguoiChotId: 1);
            Assert.True(hoaDonNhap.KiemTraDuDieuKienYeuCauThanhToan());
        }

        [Fact]
        public void HoaDon_ConfigurePartialPayment_ValidatesMinimumAmount()
        {
            var hoaDon = new HoaDon { TongTien = 5_000_000m };

            // Bật nhưng số tiền <= 0 -> Lỗi
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                hoaDon.CauHinhThanhToanMotPhan(choPhep: true, soTienToiThieu: 0m));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                hoaDon.CauHinhThanhToanMotPhan(choPhep: true, soTienToiThieu: -100_000m));

            // Bật nhưng số tiền > tổng hóa đơn -> Lỗi
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                hoaDon.CauHinhThanhToanMotPhan(choPhep: true, soTienToiThieu: 5_000_001m));

            // Hợp lệ
            hoaDon.CauHinhThanhToanMotPhan(choPhep: true, soTienToiThieu: 2_000_000m);
            Assert.True(hoaDon.ChoPhepThanhToanMotPhan);
            Assert.Equal(2_000_000m, hoaDon.SoTienThanhToanToiThieu);

            // Tắt
            hoaDon.CauHinhThanhToanMotPhan(choPhep: false, soTienToiThieu: null);
            Assert.False(hoaDon.ChoPhepThanhToanMotPhan);
            Assert.Null(hoaDon.SoTienThanhToanToiThieu);
        }
    }
}
