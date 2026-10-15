using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterAnomalyPolicyTests
    {
        private static readonly AutoInvoiceOptions Options = new();
        private const decimal Cu = 1000m;

        // Lịch sử 6 kỳ điện 38, 40, 40, 42, 45, 39 (mới → cũ): trung vị 40.
        private static readonly MeterConsumptionHistory SauKyDien = Dien(38, 40, 40, 42, 45, 39);

        private static MeterConsumptionHistory Dien(params decimal[] cacKy) =>
            new(cacKy.Select(k => new MeterConsumption(k, 0m)).ToList(), 31, 31, 10);

        private static MeterConsumptionHistory Nuoc(params decimal[] cacKy) =>
            new(cacKy.Select(k => new MeterConsumption(0m, k)).ToList(), 31, 31, 10);

        private static MeterConsumptionHistory KhongLichSu(int soNgayPhu = 31, int soNgayCuaThang = 31) =>
            new(new MeterConsumption[0], soNgayPhu, soNgayCuaThang, 10);

        private static MeterAnomalyResult? Dien(decimal tieuThu, MeterConsumptionHistory lichSu) =>
            MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, Cu, Cu + tieuThu, lichSu, Options);

        [Fact]
        public void TangSatNguong()
        {
            Assert.Null(Dien(90m, SauKyDien));

            var r = Dien(91m, SauKyDien);

            Assert.NotNull(r);
            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Equal("Điện: tiêu thụ 91 kWh, trung vị 6 kỳ của hợp đồng 40 kWh (cho phép 12–90 kWh)", r.MoTa);
        }

        [Fact]
        public void GiamSatNguong()
        {
            Assert.Null(Dien(12m, SauKyDien));

            var r11 = Dien(11m, SauKyDien);
            var r0 = Dien(0m, SauKyDien);

            Assert.Equal(MeterAnomalyReason.TieuThuThapBatThuong, r11!.LyDo);
            Assert.Equal(MeterAnomalyReason.TieuThuThapBatThuong, r0!.LyDo);
            Assert.Equal("Điện: tiêu thụ 11 kWh, trung vị 6 kỳ của hợp đồng 40 kWh (cho phép 12–90 kWh)", r11.MoTa);
        }

        [Fact]
        public void TrungViChan_LayTrungBinhHaiGiaTriGiua()
        {
            var lichSu = Dien(30, 50);

            Assert.Null(Dien(90m, lichSu));
            var r = Dien(91m, lichSu);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Contains("trung vị 2 kỳ của hợp đồng 40 kWh", r.MoTa);
        }

        [Fact]
        public void KhongXetGiam_KhiTrungViKhongVuotNguong()
        {
            Assert.Null(Dien(0m, Dien(20, 20, 20)));

            var r = Dien(6m, Dien(21, 21, 21));

            Assert.Equal(MeterAnomalyReason.TieuThuThapBatThuong, r!.LyDo);
            Assert.Equal("Điện: tiêu thụ 6 kWh, trung vị 3 kỳ của hợp đồng 21 kWh (cho phép 6,3–71 kWh)", r.MoTa);
        }

        [Fact]
        public void TrungViBangKhong()
        {
            var lichSu = Dien(0, 0, 0);

            Assert.Null(Dien(50m, lichSu));
            var r = Dien(51m, lichSu);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Equal("Điện: tiêu thụ 51 kWh, trung vị 3 kỳ của hợp đồng 0 kWh (cho phép 0–50 kWh)", r.MoTa);
        }

        [Fact]
        public void MotKy_DungTranChung()
        {
            var lichSu = Dien(40);

            Assert.Null(Dien(500m, lichSu));
            var r = Dien(501m, lichSu);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Equal("Điện: tiêu thụ 501 kWh, hợp đồng mới chưa đủ lịch sử, so với trần chung 500 kWh", r.MoTa);
        }

        [Fact]
        public void TranTheoNgay()
        {
            var lichSu = KhongLichSu(soNgayPhu: 12, soNgayCuaThang: 31);

            Assert.Null(Dien(150m, lichSu));
            var r = Dien(200m, lichSu);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Contains("193,5", r.MoTa);
        }

        [Fact]
        public void TranTheoNgay_SoSanhGiaTriChinhXac_KhongLamTron()
        {
            // Trần = 500 × 12 / 31 = 193,548...; in ra 193,5 nhưng 193,52 vẫn hợp lệ.
            var lichSu = KhongLichSu(soNgayPhu: 12, soNgayCuaThang: 31);

            Assert.Null(Dien(193.52m, lichSu));
            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, Dien(193.55m, lichSu)!.LyDo);
        }

        [Fact]
        public void NghiSaiChuSo()
        {
            var r = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, 1234m, 12345m, SauKyDien, Options);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, r!.LyDo);
            Assert.Equal("Điện: chỉ số mới (12.345) quá lớn so với chỉ số cũ (1.234), có thể đọc thừa một chữ số", r.MoTa);

            var duoi100 = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, 99m, 900m, KhongLichSu(), Options);
            Assert.NotEqual(MeterAnomalyReason.NghiSaiChuSo, duoi100?.LyDo);
        }

        [Fact]
        public void ChiSoGiam()
        {
            var r = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, 100m, 99m, SauKyDien, Options);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal("Điện: chỉ số mới (99) nhỏ hơn chỉ số cũ (100)", r.MoTa);
        }

        [Fact]
        public void Nuoc_DuLichSu_DungSanTangNuoc()
        {
            var lichSu = Nuoc(4, 4, 4);

            Assert.Null(MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 50m, 59m, lichSu, Options));
            var r = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 50m, 59.001m, lichSu, Options);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Equal("Nước: tiêu thụ 9,001 m³, trung vị 3 kỳ của hợp đồng 4 m³ (cho phép 1,2–9 m³)", r.MoTa);
        }

        [Fact]
        public void Nuoc_KhongLichSu_DungTranNuoc()
        {
            Assert.Null(MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 50m, 80m, KhongLichSu(), Options));

            var r = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 50m, 81m, KhongLichSu(), Options);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Equal("Nước: tiêu thụ 31 m³, hợp đồng mới chưa đủ lịch sử, so với trần chung 30 m³", r.MoTa);
        }

        [Fact]
        public void DungLichSuCungLoai()
        {
            // Lịch sử nước lớn không ảnh hưởng tới xét điện.
            var lichSu = new MeterConsumptionHistory(
                new[] { new MeterConsumption(40, 100), new MeterConsumption(40, 100) }, 31, 31, 10);

            Assert.Null(MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, Cu, Cu + 90m, lichSu, Options));
            Assert.Equal(MeterAnomalyReason.TieuThuThapBatThuong,
                MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 10m, 20m, lichSu, Options)!.LyDo);
        }
    }
}
