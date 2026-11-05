using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Bổ sung do Tester: ranh giới giờ VN (17:00Z), cắt công suất theo ngày tạo phòng (C1) và phương án A của X1.
    public class BaoCaoBienGioiVaPhuongAnTests
    {
        private static readonly BaoCaoPhamViDto PhamVi = new() { TenChiNhanh = "Tất cả chi nhánh" };

        private static DateTime Vn(int y, int m, int d, int h = 0, int mi = 0, int s = 0) =>
            DateTime.SpecifyKind(new DateTime(y, m, d, h, mi, s).AddHours(-7), DateTimeKind.Utc);

        private static DateTime Utc(int y, int m, int d, int h = 0, int mi = 0, int s = 0) =>
            DateTime.SpecifyKind(new DateTime(y, m, d, h, mi, s), DateTimeKind.Utc);

        private static IReadOnlyList<BaoCaoKy> Kys(int tuThang, int tuNam, int denThang, int denNam) =>
            BaoCaoCalculator.LietKeKy(new BaoCaoKy(tuThang, tuNam), new BaoCaoKy(denThang, denNam));

        private static BaoCaoHopDongRow Hd(int id, string ma, int phong, DateTime batDau, DateTime? ketThuc, TrangThaiHopDong tt = TrangThaiHopDong.DaKetThuc) =>
            new(id, ma, phong, 1, batDau, ketThuc, tt);

        private static BaoCaoPhongRow Phong(int id, DateTime ngayTao) =>
            new(id, "P" + id, 1, "A", ngayTao, TrangThaiPhong.DaThue);

        // ----- Công suất -----

        [Fact]
        public void CongSuat_HopDongBatDauTruocNgayTaoPhong_KhongVuot100PhanTramOMoiKy()
        {
            // Phòng tạo 20/03; hợp đồng nhập lùi từ 01/01 và vẫn đang chạy.
            var phongs = new[] { Phong(1, Vn(2026, 3, 20)) };
            var hopDongs = new[] { Hd(1, "HD-1", 1, Vn(2026, 1, 1), null, TrangThaiHopDong.DangHoatDong) };

            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(1, 2026, 4, 2026), false, phongs, hopDongs, Vn(2026, 8, 1));

            foreach (var ky in d.TheoKy)
            {
                Assert.True(ky.NgayPhongDaThue <= ky.NgayPhongCoTheThue, $"{ky.Nhan}: {ky.NgayPhongDaThue}/{ky.NgayPhongCoTheThue}");
                Assert.True((ky.TyLeLapDay ?? 0m) <= 100m);
            }
            // Kỳ trước ngày tạo phòng không có ngày để cho thuê.
            Assert.Equal(0, d.TheoKy[0].NgayPhongCoTheThue);
            Assert.Null(d.TheoKy[0].TyLeLapDay);
            Assert.Equal(0, d.TheoKy[1].NgayPhongCoTheThue);
            // T3: 20/03 - 31/03 = 12 ngày, đã thuê trọn 12 ngày.
            Assert.Equal(12, d.TheoKy[2].NgayPhongCoTheThue);
            Assert.Equal(12, d.TheoKy[2].NgayPhongDaThue);
            Assert.Equal(100.0m, d.Tong.TyLeLapDay);
        }

        [Fact]
        public void CongSuat_HaiHopDongNoiTiepQuaRanhGioiNam_KhongDemTrungKhongHo()
        {
            var phongs = new[] { Phong(1, Vn(2020, 1, 1)) };
            var hopDongs = new[]
            {
                // Kết thúc 31/12 23:59:59 VN, bắt đầu 01/01 00:00:00 VN.
                Hd(1, "HD-1", 1, Vn(2025, 7, 1), Utc(2025, 12, 31, 16, 59, 59)),
                Hd(2, "HD-2", 1, Utc(2025, 12, 31, 17, 0, 0), Vn(2026, 3, 31, 23, 59, 59))
            };

            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(12, 2025, 1, 2026), false, phongs, hopDongs, Vn(2026, 6, 1));

            Assert.Equal(31, d.TheoKy[0].NgayPhongDaThue);
            Assert.Equal(31, d.TheoKy[0].NgayPhongCoTheThue);
            Assert.Equal(31, d.TheoKy[1].NgayPhongDaThue);
            Assert.Equal(100.0m, d.TheoKy[0].TyLeLapDay);
            Assert.Equal(100.0m, d.TheoKy[1].TyLeLapDay);
        }

        // ----- Hợp đồng -----

        [Fact]
        public void HopDong_KetThucRanhGioiGioVn_HetHanDungKy()
        {
            var hopDongs = new[]
            {
                // 31/03 23:59:59 VN (16:59:59Z) thuộc T3.
                Hd(1, "A", 1, Vn(2025, 1, 1), Utc(2026, 3, 31, 16, 59, 59)),
                // 01/04 00:00:00 VN (17:00:00Z) thuộc T4.
                Hd(2, "B", 2, Vn(2025, 1, 1), Utc(2026, 3, 31, 17, 0, 0))
            };

            var d = BaoCaoHopDongBuilder.Build(PhamVi, Kys(3, 2026, 4, 2026), hopDongs, Vn(2026, 8, 1));

            Assert.Equal(1, d.TheoKy[0].HetHanTrongKy);
            Assert.Equal(1, d.TheoKy[1].HetHanTrongKy);
            Assert.Equal(1, d.TheoKy[0].TraPhong);
            Assert.Equal(1, d.TheoKy[1].TraPhong);
        }

        [Fact]
        public void HopDong_BatDauDauNgayVnVaGiayTruoc_RoiVaoHaiKyLienKe()
        {
            var hopDongs = new[]
            {
                Hd(1, "A", 1, Utc(2026, 2, 28, 16, 59, 59), null, TrangThaiHopDong.DangHoatDong), // 28/02 23:59:59 VN -> T2
                Hd(2, "B", 2, Utc(2026, 2, 28, 17, 0, 0), null, TrangThaiHopDong.DangHoatDong)    // 01/03 00:00:00 VN -> T3
            };

            var d = BaoCaoHopDongBuilder.Build(PhamVi, Kys(2, 2026, 3, 2026), hopDongs, Vn(2026, 8, 1));

            Assert.Equal(1, d.TheoKy[0].HopDongMoi);
            Assert.Equal(1, d.TheoKy[1].HopDongMoi);
        }

        // ----- Doanh thu: phương án A của X1 -----

        private static BaoCaoGiuChoRow GiuCho(TrangThaiYeuCauGiuCho trangThai, decimal daNhan, decimal daApDung, bool coQd,
            TrangThaiHoanTien? tt = null, DateTime? ngay = null, decimal daHoan = 0m) =>
            new(1, 1, "A", trangThai, daNhan, daApDung, coQd, tt, ngay, daHoan);

        private static BaoCaoDoanhThuDto DoanhThu(params BaoCaoGiuChoRow[] rows) =>
            BaoCaoDoanhThuBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), false,
                Array.Empty<BaoCaoHoaDonRow>(), Array.Empty<BaoCaoThanhToanRow>(), 0m, rows);

        [Theory]
        [InlineData(TrangThaiYeuCauGiuCho.MoiTao)]
        [InlineData(TrangThaiYeuCauGiuCho.ChoThanhToan)]
        [InlineData(TrangThaiYeuCauGiuCho.ChoXacNhanTien)]
        [InlineData(TrangThaiYeuCauGiuCho.DangGiuCho)]
        [InlineData(TrangThaiYeuCauGiuCho.DaChuyenHopDong)]
        public void DoanhThu_DangChoQuyetDinh_TrangThaiKhongThuocPhuongAnA_KhongTinh(TrangThaiYeuCauGiuCho trangThai)
        {
            var d = DoanhThu(GiuCho(trangThai, 500_000m, 0m, false));

            Assert.Equal(0m, d.GiuChoDangChoQuyetDinh);
        }

        [Theory]
        [InlineData(TrangThaiYeuCauGiuCho.TuChoi)]
        [InlineData(TrangThaiYeuCauGiuCho.HetHan)]
        [InlineData(TrangThaiYeuCauGiuCho.DaHuy)]
        public void DoanhThu_DangChoQuyetDinh_TrangThaiKetThucChuaCoQuyetDinh_TinhPhanDu(TrangThaiYeuCauGiuCho trangThai)
        {
            var d = DoanhThu(GiuCho(trangThai, 500_000m, 120_000m, false));

            Assert.Equal(380_000m, d.GiuChoDangChoQuyetDinh);
        }

        [Fact]
        public void DoanhThu_DangChoQuyetDinh_ApDungHetHoacVuotNhan_KhongCongSoAm()
        {
            var d = DoanhThu(
                GiuCho(TrangThaiYeuCauGiuCho.DaHuy, 500_000m, 500_000m, false),
                GiuCho(TrangThaiYeuCauGiuCho.DaHuy, 500_000m, 700_000m, false));

            Assert.Equal(0m, d.GiuChoDangChoQuyetDinh);
        }

        [Theory]
        [InlineData(TrangThaiHoanTien.ChoHoanTien)]
        [InlineData(TrangThaiHoanTien.DangHoanTien)]
        public void DoanhThu_QuyetDinhChoHoacDangHoan_KhongCongVaoThuKhacLanDangCho(TrangThaiHoanTien tt)
        {
            var d = DoanhThu(GiuCho(TrangThaiYeuCauGiuCho.DaHuy, 600_000m, 0m, true, tt, Vn(2026, 3, 5), 0m));

            Assert.Equal(0m, d.Tong.ThuKhacGiuCho);
            Assert.Equal(0m, d.GiuChoDangChoQuyetDinh);
        }

        [Fact]
        public void DoanhThu_ThuKhac_DaHoanTienVoiPhanDuAm_KhongCong()
        {
            // Đã hoàn nhiều hơn số nhận trừ áp dụng: không ghi số âm vào Thu khác.
            var d = DoanhThu(GiuCho(TrangThaiYeuCauGiuCho.DaHuy, 300_000m, 0m, true, TrangThaiHoanTien.DaHoanTien, Vn(2026, 3, 5), 300_000m));

            Assert.Equal(0m, d.Tong.ThuKhacGiuCho);
        }

        // ----- Nhãn phương thức biên lai: chuỗi cũ giữ nguyên cho mọi kênh -----

        private static string Nhan(string ma, PhuongThucThanhToan pt)
        {
            var receipt = new PaymentReceiptSnapshot
            {
                LichSuThanhToanId = 1,
                HoaDonId = 1,
                MaHoaDon = "HD-1",
                ChiNhanhId = 1,
                TenChiNhanh = "CN",
                DiaChiChiNhanh = "Dia chi",
                TenPhong = "P1",
                Thang = 3,
                Nam = 2026,
                TenKhachThue = "Khach",
                EmailKhachThue = "khach@test.vn",
                TongTien = 1_000_000m,
                SoTienThanhToan = 100_000m,
                NgayThanhToanUtc = DateTime.UtcNow,
                MaGiaoDich = ma,
                PhuongThucThanhToan = pt,
                GhiChu = "",
                LuyKeDaThanhToan = 100_000m
            };
            return PaymentNotificationFormatter.BuildReceiptEmail(receipt, null, null).PhuongThuc;
        }

        [Theory]
        [InlineData("VNPAY-123", PhuongThucThanhToan.ChuyenKhoan, "Thẻ ATM / Visa / MasterCard (VNPay)")]
        [InlineData("vnpay-123", PhuongThucThanhToan.ChuyenKhoan, "Thẻ ATM / Visa / MasterCard (VNPay)")]
        [InlineData("SEPAY-ID-9", PhuongThucThanhToan.ChuyenKhoan, "Chuyển khoản 24/7 (VietQR, tự động)")]
        [InlineData("CANTRU-HD1-2", PhuongThucThanhToan.CanTruTienCoc, "Cấn trừ tiền cọc")]
        [InlineData("TM-1", PhuongThucThanhToan.TienMat, "Tiền mặt")]
        [InlineData("FT-TAY", PhuongThucThanhToan.ChuyenKhoan, "Chuyển khoản")]
        public void BienLai_NhanPhuongThuc_GiuChuoiCu(string ma, PhuongThucThanhToan pt, string expected)
        {
            Assert.Equal(expected, Nhan(ma, pt));
        }
    }
}
