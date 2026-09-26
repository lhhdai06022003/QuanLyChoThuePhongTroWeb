using System;
using System.Linq;
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
        public void HoaDon_FullHappyPath_Nhap_To_ChoDuyet_To_DaChot_To_DaGui_Succeeds()
        {
            var hoaDon = new HoaDon { TongTien = 2_000_000m };

            // 1. Nhap -> ChoDuyet
            hoaDon.GuiDuyet(nguoiThucHienId: 10, lyDo: "Nhân viên gửi duyệt nháp");
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, hoaDon.TrangThaiPhatHanh);

            // 2. ChoDuyet -> DaChot
            hoaDon.ChotHoaDon(nguoiChotId: 5, lyDo: "Admin chốt hóa đơn");
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, hoaDon.TrangThaiPhatHanh);
            Assert.Equal(5, hoaDon.NguoiChotId);
            Assert.NotNull(hoaDon.NgayChot);

            // 3. DaChot -> DaGui
            hoaDon.GuiHoaDon(nguoiGuiId: 5, lyDo: "Công bố lên cổng khách thuê");
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, hoaDon.TrangThaiPhatHanh);
            Assert.NotNull(hoaDon.NgayGui);

            Assert.Equal(3, hoaDon.LichSuTrangThaiHoaDons.Count);
            var historyList = hoaDon.LichSuTrangThaiHoaDons.ToList();

            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, historyList[0].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, historyList[0].TrangThaiPhatHanhMoi);
            Assert.Equal(10, historyList[0].NguoiThucHienId);

            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, historyList[1].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, historyList[1].TrangThaiPhatHanhMoi);
            Assert.Equal(5, historyList[1].NguoiThucHienId);

            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, historyList[2].TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, historyList[2].TrangThaiPhatHanhMoi);
            Assert.Equal(5, historyList[2].NguoiThucHienId);
        }

        [Fact]
        public void HoaDon_TraLai_FromChoDuyet_ToNhap_WithReason_Succeeds()
        {
            var hoaDon = new HoaDon { TongTien = 1_500_000m };
            hoaDon.GuiDuyet(nguoiThucHienId: 10);

            hoaDon.TraLai(nguoiThucHienId: 1, lyDo: "Chỉ số nước chưa khớp ảnh đồng hồ");

            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, hoaDon.TrangThaiPhatHanh);
            Assert.Equal(2, hoaDon.LichSuTrangThaiHoaDons.Count);

            var traLaiHistory = hoaDon.LichSuTrangThaiHoaDons.Last();
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, traLaiHistory.TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, traLaiHistory.TrangThaiPhatHanhMoi);
            Assert.Equal("Chỉ số nước chưa khớp ảnh đồng hồ", traLaiHistory.LyDo);
            Assert.Equal(1, traLaiHistory.NguoiThucHienId);
        }

        [Fact]
        public void HoaDon_TraLai_WithoutReason_Or_NotFromChoDuyet_ThrowsException()
        {
            var hoaDon = new HoaDon { TongTien = 1_500_000m };

            // Từ Nhap không thể trả lại
            Assert.Throws<InvalidOperationException>(() => hoaDon.TraLai(1, "Lý do hợp lệ"));

            hoaDon.GuiDuyet(10);

            // Lý do rỗng hoặc null bị từ chối
            Assert.Throws<ArgumentException>(() => hoaDon.TraLai(1, ""));
            Assert.Throws<ArgumentException>(() => hoaDon.TraLai(1, "   "));
        }

        [Fact]
        public void HoaDon_ChotThangTuNhap_Or_ChotLap_ThrowsInvalidOperationException()
        {
            var hoaDon = new HoaDon { TongTien = 2_000_000m };

            // Chốt thẳng từ Nháp bị từ chối
            var exNhap = Assert.Throws<InvalidOperationException>(() => hoaDon.ChotHoaDon(nguoiChotId: 5));
            Assert.Contains("chờ duyệt", exNhap.Message, StringComparison.OrdinalIgnoreCase);

            hoaDon.GuiDuyet(10);
            hoaDon.ChotHoaDon(5);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, hoaDon.TrangThaiPhatHanh);

            // Chốt lặp khi đã chốt bị từ chối
            Assert.Throws<InvalidOperationException>(() => hoaDon.ChotHoaDon(5));
        }

        [Fact]
        public void HoaDon_GuiHoaDon_NotFromDaChot_Or_GuiLap_ThrowsInvalidOperationException()
        {
            var hoaDon = new HoaDon { TongTien = 2_000_000m };

            // Từ Nháp không thể gửi
            Assert.Throws<InvalidOperationException>(() => hoaDon.GuiHoaDon(1));

            hoaDon.GuiDuyet(10);
            // Từ Chờ duyệt không thể gửi
            Assert.Throws<InvalidOperationException>(() => hoaDon.GuiHoaDon(1));

            hoaDon.ChotHoaDon(1);
            hoaDon.GuiHoaDon(1);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, hoaDon.TrangThaiPhatHanh);

            // Gửi lặp khi đã gửi bị từ chối
            Assert.Throws<InvalidOperationException>(() => hoaDon.GuiHoaDon(1));
        }

        [Fact]
        public void HoaDon_DaHuy_IsTerminalState_CannotTransitionToAnyState_AndMarksIsDeleted()
        {
            var hoaDon = new HoaDon { TongTien = 1_000_000m };
            hoaDon.GuiDuyet(10);
            hoaDon.ChotHoaDon(1);

            hoaDon.HuyHoaDon(nguoiHuyId: 1, lyDo: "Khách hủy hợp đồng");

            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, hoaDon.TrangThaiPhatHanh);
            Assert.True(hoaDon.IsDeleted);

            // Đã hủy là trạng thái cuối: không thể chuyển sang bất kỳ trạng thái nào
            Assert.Throws<InvalidOperationException>(() => hoaDon.GuiDuyet(10));
            Assert.Throws<InvalidOperationException>(() => hoaDon.TraLai(1, "Lý do"));
            Assert.Throws<InvalidOperationException>(() => hoaDon.ChotHoaDon(1));
            Assert.Throws<InvalidOperationException>(() => hoaDon.GuiHoaDon(1));
            Assert.Throws<InvalidOperationException>(() => hoaDon.HuyHoaDon(1, "Hủy lần hai"));
        }

        [Fact]
        public void HoaDon_HuyHoaDon_OnlyAllowedWhenDaChotOrDaGui_AndNotFullyPaid()
        {
            // 1. Nhap không thể hủy
            var hoaDonNhap = new HoaDon { TongTien = 1_000_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap };
            var exNhap = Assert.Throws<InvalidOperationException>(() => hoaDonNhap.HuyHoaDon(1, "Hủy nháp"));
            Assert.Contains("đã chốt hoặc đã gửi", exNhap.Message);

            // 2. ChoDuyet không thể hủy
            var hoaDonChoDuyet = new HoaDon { TongTien = 1_000_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet };
            var exChoDuyet = Assert.Throws<InvalidOperationException>(() => hoaDonChoDuyet.HuyHoaDon(1, "Hủy chờ duyệt"));
            Assert.Contains("đã chốt hoặc đã gửi", exChoDuyet.Message);

            // 3. DaChot có thể hủy
            var hoaDonDaChot = new HoaDon { TongTien = 1_000_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot };
            hoaDonDaChot.HuyHoaDon(1, "Hủy hóa đơn đã chốt");
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, hoaDonDaChot.TrangThaiPhatHanh);
            Assert.True(hoaDonDaChot.IsDeleted);

            // 4. DaGui có thể hủy
            var hoaDonDaGui = new HoaDon { TongTien = 1_000_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui };
            hoaDonDaGui.HuyHoaDon(1, "Hủy hóa đơn đã gửi");
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, hoaDonDaGui.TrangThaiPhatHanh);
            Assert.True(hoaDonDaGui.IsDeleted);

            // 5. DaThanhToan không thể hủy dù ở DaChot hay DaGui
            var hoaDonDaThanhToan = new HoaDon
            {
                TongTien = 1_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan
            };
            var exDaThanhToan = Assert.Throws<InvalidOperationException>(() => hoaDonDaThanhToan.HuyHoaDon(1, "Hủy đã thanh toán"));
            Assert.Contains("đã thanh toán đầy đủ", exDaThanhToan.Message);
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

            hoaDonNhap.GuiDuyet(10);
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
