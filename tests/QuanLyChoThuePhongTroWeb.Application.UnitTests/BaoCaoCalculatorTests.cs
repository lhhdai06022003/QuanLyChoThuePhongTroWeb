using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class BaoCaoCalculatorTests
    {
        private static DateTime Utc(int y, int m, int d, int h = 0, int mi = 0, int s = 0) =>
            DateTime.SpecifyKind(new DateTime(y, m, d, h, mi, s), DateTimeKind.Utc);

        [Fact]
        public void LietKeKy_RaDungThuTu()
        {
            var kys = BaoCaoCalculator.LietKeKy(new BaoCaoKy(11, 2025), new BaoCaoKy(2, 2026));
            Assert.Equal(4, kys.Count);
            Assert.Equal(new[] { "T11/2025", "T12/2025", "T1/2026", "T2/2026" }, new[] { kys[0].Text, kys[1].Text, kys[2].Text, kys[3].Text });
        }

        [Fact]
        public void KiemTraKhoang_24KyHopLe_25KyBiTuChoi()
        {
            Assert.Null(BaoCaoCalculator.KiemTraKhoang(1, 2024, 12, 2025));
            Assert.Equal("Khoảng báo cáo tối đa 24 kỳ.", BaoCaoCalculator.KiemTraKhoang(1, 2024, 1, 2026));
        }

        [Fact]
        public void KiemTraKhoang_TuLonHonDen_VaThangNgoaiKhoang()
        {
            Assert.Equal("Kỳ bắt đầu phải trước hoặc bằng kỳ kết thúc.", BaoCaoCalculator.KiemTraKhoang(5, 2026, 4, 2026));
            Assert.Equal("Kỳ báo cáo không hợp lệ.", BaoCaoCalculator.KiemTraKhoang(13, 2025, 1, 2026));
            Assert.Equal("Kỳ báo cáo không hợp lệ.", BaoCaoCalculator.KiemTraKhoang(1, 1999, 1, 2026));
        }

        [Fact]
        public void TyLe_MauBangKhong_LaNull_VaLamTronMotChuSo()
        {
            Assert.Null(BaoCaoCalculator.TyLe(5m, 0m));
            Assert.Null(BaoCaoCalculator.TyLe(5, 0));
            Assert.Equal(33.3m, BaoCaoCalculator.TyLe(1, 3));
            Assert.Equal(66.7m, BaoCaoCalculator.TyLe(2m, 3m));
        }

        [Fact]
        public void KyCua_DoiSangNgayVn()
        {
            // 28/02 17:30Z là 01/03 00:30 giờ VN.
            Assert.Equal(new BaoCaoKy(3, 2026), BaoCaoCalculator.KyCua(Utc(2026, 2, 28, 17, 30)));
            Assert.Equal(new BaoCaoKy(2, 2026), BaoCaoCalculator.KyCua(Utc(2026, 2, 28, 16, 59, 59)));
        }

        [Fact]
        public void PhanNhomTuoiNo_HanNull_KhongCoHan()
        {
            Assert.Equal(NhomTuoiNo.KhongCoHan, BaoCaoCalculator.PhanNhomTuoiNo(null, Utc(2026, 5, 1)));
        }

        [Fact]
        public void PhanNhomTuoiNo_HanBangHoacSauBayGio_ChuaDenHan()
        {
            var now = Utc(2026, 5, 1, 3);
            Assert.Equal(NhomTuoiNo.ChuaDenHan, BaoCaoCalculator.PhanNhomTuoiNo(now, now));
            Assert.Equal(NhomTuoiNo.ChuaDenHan, BaoCaoCalculator.PhanNhomTuoiNo(now.AddDays(5), now));
        }

        [Fact]
        public void PhanNhomTuoiNo_QuaMotGiayCungNgayVn_LaMotNgay()
        {
            var now = Utc(2026, 5, 1, 3);
            Assert.Equal(NhomTuoiNo.Tu1Den30, BaoCaoCalculator.PhanNhomTuoiNo(now.AddSeconds(-1), now));
            Assert.Equal(1, BaoCaoCalculator.SoNgayQuaHan(now.AddSeconds(-1), now));
        }

        [Theory]
        [InlineData(30, NhomTuoiNo.Tu1Den30)]
        [InlineData(31, NhomTuoiNo.Tu31Den60)]
        [InlineData(60, NhomTuoiNo.Tu31Den60)]
        [InlineData(61, NhomTuoiNo.Tu61Den90)]
        [InlineData(90, NhomTuoiNo.Tu61Den90)]
        [InlineData(91, NhomTuoiNo.Tren90)]
        public void PhanNhomTuoiNo_TheoRanhGioi(int soNgay, NhomTuoiNo expected)
        {
            var now = Utc(2026, 6, 15, 3);
            Assert.Equal(expected, BaoCaoCalculator.PhanNhomTuoiNo(now.AddDays(-soNgay), now));
        }

        [Fact]
        public void CuaSoChoThue_CatTheoNgayTaoVaHomNay()
        {
            var ky = new BaoCaoKy(3, 2026);
            var homNay = new DateOnly(2026, 3, 20);

            var cuaSo = BaoCaoCalculator.CuaSoChoThue(new DateOnly(2026, 3, 15), ky, homNay);
            Assert.Equal((new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 20)), cuaSo!.Value);

            Assert.Null(BaoCaoCalculator.CuaSoChoThue(new DateOnly(2026, 3, 25), ky, homNay));
            Assert.Null(BaoCaoCalculator.CuaSoChoThue(new DateOnly(2025, 1, 1), new BaoCaoKy(4, 2026), homNay));

            var thangTruoc = BaoCaoCalculator.CuaSoChoThue(new DateOnly(2025, 1, 1), new BaoCaoKy(2, 2026), homNay);
            Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), thangTruoc!.Value);
        }

        [Fact]
        public void DemNgayGiao_TinhPhanGiaoCuaCacKhoang()
        {
            var khoang = new List<(DateOnly Tu, DateOnly Den)>
            {
                (new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 5)),
                (new DateOnly(2026, 2, 20), new DateOnly(2026, 3, 10))
            };

            Assert.Equal(5, BaoCaoCalculator.DemNgayGiao(khoang, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)));
            Assert.Equal(5 + 9, BaoCaoCalculator.DemNgayGiao(khoang, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
            Assert.Equal(0, BaoCaoCalculator.DemNgayGiao(khoang, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
        }

        [Fact]
        public void KhoangNgayThue_HopNhat_GopKhoangChongVaLienNhau()
        {
            var ketQua = KhoangNgayThue.HopNhat(new[]
            {
                (new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
                (new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31)),
                (new DateOnly(2027, 6, 1), new DateOnly(2027, 5, 1)),
                (new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31))
            });

            Assert.Equal(2, ketQua.Count);
            Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2027, 3, 31)), ketQua[0]);
            Assert.Equal((new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31)), ketQua[1]);
        }
    }
}
