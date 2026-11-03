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

        // ---------- Mức cảnh báo, gợi ý sửa số, quay vòng (spec ai-phan-tich-dien-nuoc mục 3) ----------

        private static MeterAnomalyResult? DienCuMoi(decimal cu, decimal moi, MeterConsumptionHistory? lichSu = null) =>
            MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, cu, moi, lichSu ?? KhongLichSu(), Options);

        [Fact]
        public void MucDo_TieuThuBatThuong_DungBienBaLanTrungVi()
        {
            // Trung vị 40: trần 90. Tiêu thụ 119 là vàng, 120 (= 3 × M) là đỏ.
            var vang = Dien(119m, SauKyDien);
            var @do = Dien(120m, SauKyDien);

            Assert.Equal(MeterAnomalyLevel.Vang, vang!.MucDo);
            Assert.Equal(MeterAnomalyLevel.Do, @do!.MucDo);
            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, @do.LyDo);
        }

        [Fact]
        public void MucDo_TieuThuBatThuong_ChuaDuLichSu_LaVang()
        {
            var r = DienCuMoi(1000m, 1600m);

            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, r!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Vang, r.MucDo);
        }

        [Fact]
        public void MucDo_TieuThuThap_LaVang_VaChiSoGiamLaChan()
        {
            Assert.Equal(MeterAnomalyLevel.Vang, Dien(11m, SauKyDien)!.MucDo);
            Assert.Equal(MeterAnomalyLevel.Chan, DienCuMoi(1000m, 900m)!.MucDo);
        }

        [Fact]
        public void TieuThuThap_BangKhong_ThemGoiYKiemTraKetSo()
        {
            var r0 = Dien(0m, SauKyDien);
            var r11 = Dien(11m, SauKyDien);

            Assert.EndsWith(", kiểm tra đồng hồ kẹt số hoặc ảnh cũ", r0!.MoTa);
            Assert.DoesNotContain("kẹt số", r11!.MoTa);
        }

        [Fact]
        public void LechDauPhay_ChiSoMoiLonHon_GoiYChiaMuoi()
        {
            var r = DienCuMoi(1500m, 16082m);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, r!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Do, r.MucDo);
            Assert.Equal(1608.2m, r.ChiSoGoiY);
            Assert.Equal(108.2m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void LechDauPhay_ChiSoGiam_GoiYNhanMuoi()
        {
            var r = DienCuMoi(520m, 53.4426m);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Chan, r.MucDo);
            Assert.Equal(534.426m, r.ChiSoGoiY);
            Assert.Equal(14.426m, r.SanLuongTheoGoiY);
            Assert.Null(r.TranCongTo);
        }

        [Fact]
        public void QuayVong_9950Xuong100_GoiYSanLuong150_KhongGoiYChiSo()
        {
            var r = DienCuMoi(9950m, 100m);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Chan, r.MucDo);
            Assert.Equal(10_000m, r.TranCongTo);
            Assert.Equal(150m, r.SanLuongTheoGoiY);
            Assert.Null(r.ChiSoGoiY);
        }

        [Fact]
        public void QuayVong_SanLuongVuotTran_KhongGoiYQuayVong()
        {
            // Cũ 9950 → mới 700: (10000 − 9950) + 700 = 750 > trần 500, không phải quay vòng.
            var r = DienCuMoi(9950m, 700m);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Null(r.TranCongTo);
        }

        [Fact]
        public void NghiSaiChuSo_KhongUngVien_KhongGoiY()
        {
            var r = DienCuMoi(150m, 1500m);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, r!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Do, r.MucDo);
            Assert.Null(r.ChiSoGoiY);
            Assert.Null(r.SanLuongTheoGoiY);
        }

        [Fact]
        public void TieuThuVuotTran_CoUngVienChiaMuoi_DoiSangNghiSaiChuSo()
        {
            // Cũ 50 (< 100) nên không vào điều kiện ×9, nhưng tiêu thụ 1.234,5 vượt trần và 128,45 hợp lý.
            var r = DienCuMoi(50m, 1284.5m);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, r!.LyDo);
            Assert.Equal(128.45m, r.ChiSoGoiY);
            Assert.Equal(78.45m, r.SanLuongTheoGoiY);
            Assert.Contains("có thể đọc thừa một chữ số", r.MoTa);
        }

        [Theory]
        [InlineData(9950, 10_000)]
        [InlineData(10_000, 100_000)]
        [InlineData(50, 10_000)]
        [InlineData(99_999.5, 100_000)]
        public void TinhTranCongTo(double cu, double expected)
        {
            Assert.Equal((decimal)expected, MeterAnomalyPolicy.TinhTranCongTo((decimal)cu));
        }

        // ---------- Bổ sung của Tester: điều kiện quay vòng, thứ tự, biên, loại nước ----------

        // Lịch sử đủ kỳ với trung vị 1.000: trần cho phép max(2.000; 1.050) = 2.000, đủ lớn để quay vòng và lệch dấu phẩy cùng khả dĩ.
        private static readonly MeterConsumptionHistory LichSuLon = Dien(1000, 1000, 1000);

        [Fact]
        public void QuayVong_DungChinXacChinCapPhanTramTran_LaQuayVong()
        {
            // 9.000 = đúng 90% của 10.000; (10.000 − 9.000) + 100 = 1.100 ≤ 2.000.
            var r = DienCuMoi(9000m, 100m, LichSuLon);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(10_000m, r.TranCongTo);
            Assert.Equal(1100m, r.SanLuongTheoGoiY);
            Assert.Null(r.ChiSoGoiY);
        }

        [Fact]
        public void QuayVong_DuoiChinMuoiPhanTramTran_KhongLaQuayVong()
        {
            var r = DienCuMoi(8999m, 100m, LichSuLon);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Chan, r.MucDo);
            Assert.Null(r.TranCongTo);
        }

        [Fact]
        public void QuayVong_TranTangLenNamChuSo_KhiChiSoCuDaVuot10000()
        {
            // Cũ 95.000 thì trần là 100.000 (không phải 10.000); (100.000 − 95.000) + 50 = 5.050.
            var lichSu = Dien(6000, 6000, 6000);

            var r = DienCuMoi(95_000m, 50m, lichSu);

            Assert.Equal(100_000m, r!.TranCongTo);
            Assert.Equal(5050m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void QuayVong_ChiSoCuDuoiChinMuoiPhanTramCuaTranNamChuSo_KhongLaQuayVong()
        {
            // Cũ 50.000: trần 100.000, chỉ 50%. Không được hiểu là quay vòng của trần 10.000.
            var r = DienCuMoi(50_000m, 100m, Dien(6000, 6000, 6000));

            Assert.Null(r!.TranCongTo);
        }

        [Fact]
        public void QuayVongDuocXetTruocLechDauPhay_KhiCaHaiDeuKhaDi()
        {
            // Cũ 9.500, mới 960: quay vòng cho sản lượng 1.460; nhân 10 cũng cho 9.600 (sản lượng 100). Quay vòng phải thắng.
            var r = DienCuMoi(9500m, 960m, LichSuLon);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(10_000m, r.TranCongTo);
            Assert.Equal(1460m, r.SanLuongTheoGoiY);
            Assert.Null(r.ChiSoGoiY);
        }

        [Fact]
        public void QuayVong_9950Xuong100_KhongGoiYSuaThanh10000_DuCoLichSuLon()
        {
            // Với trần rộng, nhân 100 cũng cho 10.000 (sản lượng 50) nhưng đó là gợi ý sai: đây là quay vòng 150.
            var r = DienCuMoi(9950m, 100m, LichSuLon);

            Assert.Null(r!.ChiSoGoiY);
            Assert.Equal(150m, r.SanLuongTheoGoiY);
            Assert.Equal(10_000m, r.TranCongTo);
        }

        [Fact]
        public void QuayVong_TranTheoNgay_ChiSoVuotTranCoDinhThiKhongGoiY()
        {
            // Hợp đồng mới chưa đủ lịch sử, mới phủ 12/31 ngày: trần 193,5. Quay vòng 150 hợp lệ; chỉ phủ 3/31 ngày thì trần 48,4 < 150.
            var phu12 = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, 9950m, 100m, KhongLichSu(12, 31), Options);
            var phu3 = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, 9950m, 100m, KhongLichSu(3, 31), Options);

            Assert.Equal(10_000m, phu12!.TranCongTo);
            Assert.Null(phu3!.TranCongTo);
        }

        [Fact]
        public void QuayVong_Nuoc_DungTranChungNuoc()
        {
            // Nước 9.990 → 3: (10.000 − 9.990) + 3 = 13 ≤ 30.
            var r = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 9990m, 3m, KhongLichSu(), Options);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(10_000m, r.TranCongTo);
            Assert.Equal(13m, r.SanLuongTheoGoiY);
            Assert.Contains("Nước:", r.MoTa);
        }

        [Fact]
        public void LechDauPhay_Nuoc_GoiYNhanMuoi()
        {
            var r = MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, 50m, 5.5m, KhongLichSu(), Options);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Equal(55m, r.ChiSoGoiY);
            Assert.Equal(5m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void LechDauPhay_ChiSoGiam_ChonHeSoNhoNhatKhaDi()
        {
            // Cũ 300, mới 5: nhân 10 = 50 < 300 (loại), nhân 100 = 500 → sản lượng 200 ≤ 500.
            var r = DienCuMoi(300m, 5m);

            Assert.Equal(500m, r!.ChiSoGoiY);
            Assert.Equal(200m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void LechDauPhay_ChiSoGiam_KhongUngVienKhaDi_ChiBaoSoGiam()
        {
            // Cũ 5.000, mới 5: nhân 100 = 500 vẫn < 5.000.
            var r = DienCuMoi(5000m, 5m);

            Assert.Equal(MeterAnomalyReason.ChiSoGiam, r!.LyDo);
            Assert.Null(r.ChiSoGoiY);
            Assert.Null(r.SanLuongTheoGoiY);
            Assert.Null(r.TranCongTo);
        }

        [Fact]
        public void NghiSaiChuSo_ChiaMuoi_ChonHeSoNhoNhatKhaDi()
        {
            // Cũ 20, mới 7.000: chia 10 = 700 (sản lượng 680 > 500, loại), chia 100 = 70 (sản lượng 50).
            var r = DienCuMoi(20m, 7000m);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, r!.LyDo);
            Assert.Equal(70m, r.ChiSoGoiY);
            Assert.Equal(50m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void NghiSaiChuSo_ChiaNghin_KhiChiaMuoiVaChiaTramDeuQuaLon()
        {
            // Cũ 20, mới 70.000: 7.000 (6.980), 700 (680) đều > 500; chia 1.000 = 70 (sản lượng 50).
            var r = DienCuMoi(20m, 70_000m);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, r!.LyDo);
            Assert.Equal(70m, r.ChiSoGoiY);
            Assert.Equal(50m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void NghiSaiChuSo_DieuKienGapChin_DungBien()
        {
            // Cũ 100: mới 900 (= 9 × cũ) vào điều kiện, 899,99 thì chưa; cả hai đều không có ứng viên chia hợp lệ.
            var tai = DienCuMoi(100m, 900m);
            var duoi = DienCuMoi(100m, 899.99m);

            Assert.Equal(MeterAnomalyReason.NghiSaiChuSo, tai!.LyDo);
            Assert.Null(tai.ChiSoGoiY);
            // 899,99 − 100 = 799,99 > trần chung 500: vẫn bất thường nhưng theo luồng tiêu thụ, không phải sai chữ số.
            Assert.Equal(MeterAnomalyReason.TieuThuBatThuong, duoi!.LyDo);
            Assert.Equal(MeterAnomalyLevel.Vang, duoi.MucDo);
        }

        [Fact]
        public void NghiSaiChuSo_CuDuoi100_KhongVaoDieuKienGapChin()
        {
            var r = DienCuMoi(99m, 891m);

            Assert.NotEqual(MeterAnomalyReason.NghiSaiChuSo, r?.LyDo);
        }

        [Fact]
        public void UngVienLamTronBaChuSoThapPhan()
        {
            // Mới 1,23456 (5 chữ số thập phân): ×10 = 12,3456 không dùng được (nhỏ hơn cũ); ×100 = 123,456 đúng 3 chữ số.
            var r = DienCuMoi(100m, 1.23456m);

            Assert.Equal(123.456m, r!.ChiSoGoiY);
            Assert.Equal(23.456m, r.SanLuongTheoGoiY);
        }

        [Fact]
        public void KhongBaoSaiChuSo_KhiTieuThuBinhThuong()
        {
            Assert.Null(DienCuMoi(1000m, 1100m));
            Assert.Null(DienCuMoi(1000m, 1000m, KhongLichSu()));
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
