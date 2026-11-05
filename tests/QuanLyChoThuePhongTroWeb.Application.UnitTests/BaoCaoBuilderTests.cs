using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class BaoCaoBuilderTests
    {
        private static readonly BaoCaoPhamViDto PhamVi = new() { TenChiNhanh = "Tất cả chi nhánh" };

        // Giờ VN -> UTC.
        private static DateTime Vn(int y, int m, int d, int h = 0, int mi = 0, int s = 0) =>
            DateTime.SpecifyKind(new DateTime(y, m, d, h, mi, s).AddHours(-7), DateTimeKind.Utc);

        private static DateTime Utc(int y, int m, int d, int h = 0, int mi = 0, int s = 0) =>
            DateTime.SpecifyKind(new DateTime(y, m, d, h, mi, s), DateTimeKind.Utc);

        private static IReadOnlyList<BaoCaoKy> Kys(int tuThang, int tuNam, int denThang, int denNam) =>
            BaoCaoCalculator.LietKeKy(new BaoCaoKy(tuThang, tuNam), new BaoCaoKy(denThang, denNam));

        // ----- Doanh thu -----

        private static BaoCaoThanhToanRow Tt(int hoaDonId, decimal soTien, PhuongThucThanhToan pt, string ma, string ghiChu = "") =>
            new(hoaDonId, soTien, pt, ma, ghiChu);

        [Fact]
        public void DoanhThu_TachKenhVaCanTru_KyTrongBangKhong()
        {
            var kys = Kys(2, 2026, 4, 2026);
            var hoaDons = new[] { new BaoCaoHoaDonRow(1, 3, 2026, 1, "A", 1_000_000m) };
            var thanhToans = new[]
            {
                Tt(1, 100_000m, PhuongThucThanhToan.TienMat, "TM-1"),
                Tt(1, 200_000m, PhuongThucThanhToan.ChuyenKhoan, "CK-1"),
                Tt(1, 300_000m, PhuongThucThanhToan.ChuyenKhoan, "SEPAY-A"),
                Tt(1, 150_000m, PhuongThucThanhToan.ChuyenKhoan, "VNPAY-9"),
                Tt(1, 50_000m, PhuongThucThanhToan.CanTruTienCoc, "CANTRU-HD1-1")
            };

            var d = BaoCaoDoanhThuBuilder.Build(PhamVi, kys, true, hoaDons, thanhToans, 700_000m, Array.Empty<BaoCaoGiuChoRow>());

            var t3 = d.TheoKy.Single(k => k.Thang == 3);
            Assert.Equal(1_000_000m, t3.PhatSinh);
            Assert.Equal(800_000m, t3.DaThu);
            Assert.Equal(750_000m, t3.ThuTien);
            Assert.Equal(50_000m, t3.CanTruCoc);
            Assert.Equal(200_000m, t3.ConNo);
            Assert.Equal(100_000m, t3.TienMat);
            Assert.Equal(200_000m, t3.ChuyenKhoan);
            Assert.Equal(300_000m, t3.SePay);
            Assert.Equal(150_000m, t3.VnPay);
            Assert.Equal(80.0m, t3.TyLeThu);
            Assert.Equal(700_000m, d.TienCocDangGiu);

            var t2 = d.TheoKy.Single(k => k.Thang == 2);
            Assert.Equal(0m, t2.PhatSinh);
            Assert.Null(t2.TyLeThu);
            Assert.Equal(3, d.TheoKy.Count);
            Assert.Equal(800_000m, d.Tong.DaThu);
            Assert.Single(d.TheoChiNhanh);
            Assert.Equal("A", d.TheoChiNhanh[0].Nhan);
        }

        [Fact]
        public void DoanhThu_TienThuaDocTuTheGhiChu_VaKhongCoBangChiNhanhKhiLocMotChiNhanh()
        {
            var hoaDons = new[] { new BaoCaoHoaDonRow(1, 3, 2026, 1, "A", 100_000m) };
            var thanhToans = new[] { Tt(1, 100_000m, PhuongThucThanhToan.TienMat, "TM-1", "[TIEN-THUA:50000] khách đưa dư") };

            var d = BaoCaoDoanhThuBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), false, hoaDons, thanhToans, 0m, Array.Empty<BaoCaoGiuChoRow>());

            Assert.Equal(50_000m, d.Tong.TienThua);
            Assert.Equal(100_000m, d.Tong.DaThu);
            Assert.Equal(0m, d.Tong.ConNo);
            Assert.Empty(d.TheoChiNhanh);
        }

        private static BaoCaoGiuChoRow GiuCho(
            decimal daNhan, decimal daApDung, bool coQuyetDinh, TrangThaiHoanTien? tt, DateTime? ngay, decimal daHoan,
            TrangThaiYeuCauGiuCho trangThai = TrangThaiYeuCauGiuCho.DaHuy) =>
            new(1, 1, "A", trangThai, daNhan, daApDung, coQuyetDinh, tt, ngay, daHoan);

        [Fact]
        public void DoanhThu_ThuKhac_QuyetDinhDaHoanTien_TinhTheoNgayQuyetDinh()
        {
            var giuCho = GiuCho(500_000m, 0m, true, TrangThaiHoanTien.DaHoanTien, Vn(2026, 3, 10), 200_000m);
            var d = BaoCaoDoanhThuBuilder.Build(PhamVi, Kys(2, 2026, 4, 2026), true, Array.Empty<BaoCaoHoaDonRow>(), Array.Empty<BaoCaoThanhToanRow>(), 0m, new[] { giuCho });

            Assert.Equal(300_000m, d.TheoKy.Single(k => k.Thang == 3).ThuKhacGiuCho);
            Assert.Equal(300_000m, d.Tong.ThuKhacGiuCho);
            Assert.Equal(300_000m, d.TheoChiNhanh.Single().ThuKhacGiuCho);
        }

        [Fact]
        public void DoanhThu_ThuKhac_QuyetDinhChuaHoan_KhongTinh_VaKyNgoaiKhoangKhongTinh()
        {
            var choHoan = GiuCho(500_000m, 0m, true, TrangThaiHoanTien.ChoHoanTien, Vn(2026, 3, 10), 0m);
            var dangHoan = GiuCho(500_000m, 0m, true, TrangThaiHoanTien.DangHoanTien, Vn(2026, 3, 10), 0m);
            var ngoaiKhoang = GiuCho(500_000m, 0m, true, TrangThaiHoanTien.DaHoanTien, Vn(2026, 7, 10), 0m);
            var d = BaoCaoDoanhThuBuilder.Build(PhamVi, Kys(2, 2026, 4, 2026), true, Array.Empty<BaoCaoHoaDonRow>(), Array.Empty<BaoCaoThanhToanRow>(), 0m, new[] { choHoan, dangHoan, ngoaiKhoang });

            Assert.Equal(0m, d.Tong.ThuKhacGiuCho);
            // Đã có quyết định thì không nằm trong số "đang chờ quyết định" (phương án A).
            Assert.Equal(0m, d.GiuChoDangChoQuyetDinh);
        }

        [Fact]
        public void DoanhThu_ThuKhac_NgayQuyetDinhRanhGioiGioVn()
        {
            // 28/02 17:30Z = 01/03 00:30 VN, thuộc T3.
            var giuCho = GiuCho(400_000m, 0m, true, TrangThaiHoanTien.DaHoanTien, Utc(2026, 2, 28, 17, 30), 0m);
            var d = BaoCaoDoanhThuBuilder.Build(PhamVi, Kys(2, 2026, 3, 2026), false, Array.Empty<BaoCaoHoaDonRow>(), Array.Empty<BaoCaoThanhToanRow>(), 0m, new[] { giuCho });

            Assert.Equal(0m, d.TheoKy.Single(k => k.Thang == 2).ThuKhacGiuCho);
            Assert.Equal(400_000m, d.TheoKy.Single(k => k.Thang == 3).ThuKhacGiuCho);
        }

        [Fact]
        public void DoanhThu_DangChoQuyetDinh_ChiYeuCauChuaCoQuyetDinhVaTrangThaiKetThuc()
        {
            var rows = new[]
            {
                GiuCho(500_000m, 0m, false, null, null, 0m, TrangThaiYeuCauGiuCho.TuChoi),
                GiuCho(300_000m, 100_000m, false, null, null, 0m, TrangThaiYeuCauGiuCho.HetHan),
                GiuCho(200_000m, 0m, false, null, null, 0m, TrangThaiYeuCauGiuCho.DaHuy),
                GiuCho(900_000m, 0m, false, null, null, 0m, TrangThaiYeuCauGiuCho.DangGiuCho),
                GiuCho(900_000m, 900_000m, false, null, null, 0m, TrangThaiYeuCauGiuCho.DaHuy),
                GiuCho(900_000m, 0m, true, TrangThaiHoanTien.ChoHoanTien, Vn(2026, 3, 1), 0m, TrangThaiYeuCauGiuCho.DaHuy)
            };
            var d = BaoCaoDoanhThuBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), false, Array.Empty<BaoCaoHoaDonRow>(), Array.Empty<BaoCaoThanhToanRow>(), 0m, rows);

            Assert.Equal(500_000m + 200_000m + 200_000m, d.GiuChoDangChoQuyetDinh);
        }

        // ----- Công nợ -----

        private static BaoCaoHoaDonNoRow No(int id, int thang, int nam, DateTime? han, decimal tong, decimal daThu, int khach, string ten = "Khách", string phong = "P1", bool traPhong = false, int cn = 1, string tenCn = "A") =>
            new(id, thang, nam, han, tong, daThu, khach, ten, phong, cn, tenCn, traPhong);

        [Fact]
        public void CongNo_GomTheoKhachVaCoDaTraPhong()
        {
            var now = Utc(2026, 6, 15, 3);
            var rows = new[]
            {
                No(1, 1, 2026, now.AddDays(-100), 1_000_000m, 0m, 7, "An", "P1", true),
                No(2, 2, 2026, now.AddDays(-40), 500_000m, 100_000m, 7, "An", "P1", true),
                No(3, 5, 2026, now.AddDays(10), 300_000m, 0m, 8, "Binh", "P2"),
                No(4, 5, 2026, null, 200_000m, 0m, 9, "Chi", "P3", false),
                No(5, 4, 2026, now.AddDays(-5), 100_000m, 100_000m, 9, "Chi", "P3")
            };

            var d = BaoCaoCongNoBuilder.Build(PhamVi, rows, now);

            Assert.Equal(1_000_000m + 400_000m + 300_000m + 200_000m, d.TongConNo);
            Assert.Equal(1_400_000m, d.QuaHan);
            Assert.Equal(1_400_000m, d.NoKhachDaTraPhong);
            Assert.Equal(4, d.SoHoaDonNo);
            Assert.Equal(2, d.SoHoaDonQuaHan);
            Assert.Equal(3, d.SoKhachNo);
            Assert.Equal(6, d.NhomTuoiNo.Count);
            Assert.Equal(new[] { "ChuaDenHan", "Tu1Den30", "Tu31Den60", "Tu61Den90", "Tren90", "KhongCoHan" }, d.NhomTuoiNo.Select(n => n.Ma).ToArray());
            Assert.Equal(300_000m, d.NhomTuoiNo[0].SoTien);
            Assert.Equal(400_000m, d.NhomTuoiNo[2].SoTien);
            Assert.Equal(1_000_000m, d.NhomTuoiNo[4].SoTien);
            Assert.Equal(200_000m, d.NhomTuoiNo[5].SoTien);

            var an = d.TheoKhach.First();
            Assert.Equal("An", an.HoVaTen);
            Assert.Equal(1_400_000m, an.TongNo);
            Assert.Equal("T1/2026", an.KyNoCuNhat);
            Assert.Equal(100, an.SoNgayQuaHanLonNhat);
            Assert.True(an.DaTraPhong);
            Assert.Null(d.TheoKhach.Single(k => k.HoVaTen == "Binh").SoNgayQuaHanLonNhat);
            Assert.False(d.TheoKhach.Single(k => k.HoVaTen == "Chi").DaTraPhong);
        }

        [Fact]
        public void CongNo_KhachCoCaHoaDonTraPhongVaKhongTraPhong_KhongDuocDanhDauDaTraPhong()
        {
            var now = Utc(2026, 6, 15, 3);
            var rows = new[]
            {
                No(1, 1, 2026, now.AddDays(-10), 100_000m, 0m, 7, traPhong: true),
                No(2, 2, 2026, now.AddDays(-5), 100_000m, 0m, 7, traPhong: false)
            };
            var d = BaoCaoCongNoBuilder.Build(PhamVi, rows, now);

            Assert.False(d.TheoKhach.Single().DaTraPhong);
            Assert.Equal(100_000m, d.NoKhachDaTraPhong);
        }

        // ----- Công suất -----

        private static BaoCaoHopDongRow Hd(int id, string ma, int phong, DateTime batDau, DateTime? ketThuc, TrangThaiHopDong tt = TrangThaiHopDong.DaKetThuc, int cn = 1) =>
            new(id, ma, phong, cn, batDau, ketThuc, tt);

        private static BaoCaoPhongRow Phong(int id, DateTime ngayTao, TrangThaiPhong tt = TrangThaiPhong.DaThue, int cn = 1, string ten = "A") =>
            new(id, "P" + id, cn, ten, ngayTao, tt);

        [Fact]
        public void CongSuat_HaiHopDongNoiTiep_KhongDemTrungVaKhongHoNgay()
        {
            var phongs = new[] { Phong(1, Vn(2025, 1, 1)) };
            var hopDongs = new[]
            {
                Hd(1, "HD-1", 1, Vn(2025, 1, 1), Vn(2025, 12, 31, 23, 59, 59)),
                Hd(2, "HD-1-GH1", 1, Vn(2026, 1, 1), null, TrangThaiHopDong.DangHoatDong)
            };
            var now = Vn(2026, 6, 15, 10);

            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(12, 2025, 1, 2026), false, phongs, hopDongs, now);

            Assert.Equal(31, d.TheoKy[0].NgayPhongCoTheThue);
            Assert.Equal(31, d.TheoKy[0].NgayPhongDaThue);
            Assert.Equal(100.0m, d.TheoKy[0].TyLeLapDay);
            Assert.Equal(31, d.TheoKy[1].NgayPhongDaThue);
        }

        [Fact]
        public void CongSuat_HaiHopDongChongNhau_KhongDemHaiLan()
        {
            var phongs = new[] { Phong(1, Vn(2025, 1, 1)) };
            var hopDongs = new[]
            {
                Hd(1, "HD-1", 1, Vn(2026, 3, 1), Vn(2026, 3, 20)),
                Hd(2, "HD-2", 1, Vn(2026, 3, 10), Vn(2026, 3, 31))
            };
            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), false, phongs, hopDongs, Vn(2026, 6, 15));

            Assert.Equal(31, d.TheoKy[0].NgayPhongDaThue);
            Assert.Equal(31, d.TheoKy[0].NgayPhongCoTheThue);
        }

        [Fact]
        public void CongSuat_PhongTaoNgay15_ChiTinhTuNgayTao()
        {
            var phongs = new[] { Phong(1, Vn(2026, 3, 15)) };
            var hopDongs = new[] { Hd(1, "HD-1", 1, Vn(2026, 3, 1), Vn(2026, 3, 31)) };
            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), false, phongs, hopDongs, Vn(2026, 6, 15));

            // Cửa sổ 15/3 - 31/3 = 17 ngày; hợp đồng nhập lùi từ 01/3 không làm tỷ lệ vượt 100%.
            Assert.Equal(17, d.TheoKy[0].NgayPhongCoTheThue);
            Assert.Equal(17, d.TheoKy[0].NgayPhongDaThue);
            Assert.Equal(100.0m, d.TheoKy[0].TyLeLapDay);
        }

        [Fact]
        public void CongSuat_ThangHienTaiCatOHomNay_ThangTuongLaiBangKhong()
        {
            var phongs = new[] { Phong(1, Vn(2025, 1, 1)) };
            var hopDongs = new[] { Hd(1, "HD-1", 1, Vn(2026, 6, 1), null, TrangThaiHopDong.DangHoatDong) };
            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(6, 2026, 7, 2026), false, phongs, hopDongs, Vn(2026, 6, 10, 9));

            Assert.Equal(10, d.TheoKy[0].NgayPhongCoTheThue);
            Assert.Equal(10, d.TheoKy[0].NgayPhongDaThue);
            Assert.Equal(0, d.TheoKy[1].NgayPhongCoTheThue);
            Assert.Null(d.TheoKy[1].TyLeLapDay);
        }

        [Fact]
        public void CongSuat_NgayKetThucLuu165959Z_DuocTinhTronNgay()
        {
            var phongs = new[] { Phong(1, Vn(2025, 1, 1)) };
            // 31/03 23:59:59 VN = 31/03 16:59:59Z.
            var hopDongs = new[] { Hd(1, "HD-1", 1, Vn(2026, 3, 1), Utc(2026, 3, 31, 16, 59, 59)) };
            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(3, 2026, 4, 2026), false, phongs, hopDongs, Vn(2026, 8, 1));

            Assert.Equal(31, d.TheoKy[0].NgayPhongDaThue);
            Assert.Equal(0, d.TheoKy[1].NgayPhongDaThue);
        }

        [Fact]
        public void CongSuat_DemTheTrangThai_VaPhongTrongLauNhat()
        {
            var now = Vn(2026, 6, 15, 10);
            var phongs = new[]
            {
                Phong(1, Vn(2025, 1, 1), TrangThaiPhong.Trong),
                Phong(2, Vn(2026, 5, 1), TrangThaiPhong.Trong),
                Phong(3, Vn(2025, 1, 1), TrangThaiPhong.DaThue),
                Phong(4, Vn(2025, 1, 1), TrangThaiPhong.BaoTri)
            };
            var hopDongs = new[] { Hd(1, "HD-1", 1, Vn(2025, 1, 1), Vn(2026, 6, 5, 23, 59, 59)) };
            var d = BaoCaoCongSuatBuilder.Build(PhamVi, Kys(6, 2026, 6, 2026), true, phongs, hopDongs, now);

            Assert.Equal(4, d.TongPhong);
            Assert.Equal(1, d.DangThue);
            Assert.Equal(2, d.Trong);
            Assert.Equal(1, d.BaoTri);
            Assert.Equal(2, d.PhongTrongLauNhat.Count);
            // Phòng 2 trống từ ngày tạo 01/05 (45 ngày), phòng 1 từ khi hợp đồng kết thúc 05/06 (10 ngày).
            Assert.Equal("P2", d.PhongTrongLauNhat[0].SoPhong);
            Assert.Equal(45, d.PhongTrongLauNhat[0].SoNgayTrong);
            Assert.Equal("01/05/2026", d.PhongTrongLauNhat[0].TuNgayText);
            Assert.Equal(10, d.PhongTrongLauNhat[1].SoNgayTrong);
            Assert.Single(d.TheoChiNhanh);
        }

        // ----- Hợp đồng -----

        [Fact]
        public void HopDong_MoiGiaHanTraPhong_VaTyLeGiaHan()
        {
            var now = Vn(2026, 6, 15, 10);
            var hopDongs = new[]
            {
                Hd(1, "HD-A", 1, Vn(2026, 1, 5), Vn(2026, 3, 31, 23, 59, 59)),                              // mới T1, hết hạn T3, có gia hạn
                Hd(2, "HD-A-GH1", 1, Vn(2026, 4, 1), Vn(2026, 4, 30, 23, 59, 59)),                          // gia hạn T4, hết hạn T4, trả phòng
                Hd(3, "HD-B", 2, Vn(2026, 2, 1), Vn(2026, 3, 31, 23, 59, 59))                                // mới T2, hết hạn T3, trả phòng
            };
            var d = BaoCaoHopDongBuilder.Build(PhamVi, Kys(1, 2026, 4, 2026), hopDongs, now);

            Assert.Equal(1, d.TheoKy[0].HopDongMoi);
            Assert.Equal(1, d.TheoKy[1].HopDongMoi);
            Assert.Equal(1, d.TheoKy[3].GiaHan);

            var t3 = d.TheoKy[2];
            Assert.Equal(2, t3.HetHanTrongKy);
            Assert.Equal(2, t3.HetHanDaKetThuc);
            Assert.Equal(1, t3.HetHanCoGiaHan);
            Assert.Equal(1, t3.TraPhong);
            Assert.Equal(50.0m, t3.TyLeGiaHan);

            Assert.Equal(2, d.Tong.TraPhong);
            Assert.Equal(2, d.Tong.HopDongMoi);
            Assert.Equal(1, d.Tong.GiaHan);
        }

        [Fact]
        public void HopDong_BatDauRanhGioiGioVn_ThuocKySau()
        {
            var hopDongs = new[] { Hd(1, "HD-A", 1, Utc(2026, 2, 28, 17, 0, 0), null, TrangThaiHopDong.DangHoatDong) };
            var d = BaoCaoHopDongBuilder.Build(PhamVi, Kys(2, 2026, 3, 2026), hopDongs, Vn(2026, 6, 15));

            Assert.Equal(0, d.TheoKy[0].HopDongMoi);
            Assert.Equal(1, d.TheoKy[1].HopDongMoi);
        }

        [Fact]
        public void HopDong_QuaHanVaSapHetHan_TheoMocDauNgayVn()
        {
            var now = Vn(2026, 6, 15, 10);
            var moc = Vn(2026, 6, 15);
            var hopDongs = new[]
            {
                Hd(1, "A", 1, Vn(2026, 1, 1), moc.AddSeconds(-1), TrangThaiHopDong.DangHoatDong),   // quá hạn
                Hd(2, "B", 2, Vn(2026, 1, 1), moc, TrangThaiHopDong.DangHoatDong),                   // sắp hết hạn (đúng mốc)
                Hd(3, "C", 3, Vn(2026, 1, 1), moc.AddDays(31).AddSeconds(-1), TrangThaiHopDong.DangHoatDong), // sắp hết hạn
                Hd(4, "D", 4, Vn(2026, 1, 1), moc.AddDays(31), TrangThaiHopDong.DangHoatDong),       // chưa
                Hd(5, "E", 5, Vn(2026, 1, 1), moc.AddDays(-5), TrangThaiHopDong.DaKetThuc)           // không phải đang hoạt động
            };
            var d = BaoCaoHopDongBuilder.Build(PhamVi, Kys(6, 2026, 6, 2026), hopDongs, now);

            Assert.Equal(4, d.DangHoatDong);
            Assert.Equal(1, d.QuaHanChuaXuLy);
            Assert.Equal(2, d.SapHetHan);
        }

        [Fact]
        public void HopDong_MauSoBangKhong_TyLeNull_VaDaKetThucCoKeTiepKhongTinhTraPhong()
        {
            var hopDongs = new[]
            {
                Hd(1, "HD-A", 1, Vn(2026, 1, 1), Vn(2026, 3, 31, 23, 59, 59), TrangThaiHopDong.DangHoatDong)
            };
            var d = BaoCaoHopDongBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), hopDongs, Vn(2026, 6, 15));
            Assert.Null(d.TheoKy[0].TyLeGiaHan);

            var coKeTiep = new[]
            {
                Hd(1, "HD-A", 1, Vn(2026, 1, 1), Vn(2026, 3, 31, 23, 59, 59)),
                Hd(2, "HD-A-GH1", 1, Vn(2026, 4, 1), null, TrangThaiHopDong.DangHoatDong)
            };
            var d2 = BaoCaoHopDongBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), coKeTiep, Vn(2026, 6, 15));
            Assert.Equal(0, d2.TheoKy[0].TraPhong);
            Assert.Equal(1, d2.TheoKy[0].HetHanCoGiaHan);
            Assert.Equal(100.0m, d2.TheoKy[0].TyLeGiaHan);
        }

        // ----- Điện nước -----

        private static BaoCaoDienNuocRow Dn(int phong, int thang, int nam, decimal dienCu, decimal dienMoi, decimal giaDien, decimal nuocCu, decimal nuocMoi, decimal giaNuoc) =>
            new(phong, "P" + phong, 1, "A", thang, nam, dienCu, dienMoi, giaDien, nuocCu, nuocMoi, giaNuoc);

        [Fact]
        public void DienNuoc_BanGhiHieuAm_BiBoQuaVaDuocDem()
        {
            var rows = new[]
            {
                Dn(1, 3, 2026, 100, 200, 3000m, 10, 20, 15000m),
                Dn(2, 3, 2026, 500, 100, 3000m, 10, 20, 15000m), // điện giảm
                Dn(3, 3, 2026, 100, 150, 3000m, 50, 40, 15000m)  // nước giảm
            };
            var d = BaoCaoDienNuocBuilder.Build(PhamVi, Kys(3, 2026, 3, 2026), rows);

            Assert.Equal(1, d.SoBanGhi);
            Assert.Equal(2, d.SoBanGhiBoQua);
            Assert.Equal(100m, d.TongKwh);
            Assert.Equal(10m, d.TongM3);
            Assert.Equal(2, d.TheoKy[0].SoBanGhiBoQua);
            Assert.Equal(100m, d.TrungBinhKwhPhongThang);
        }

        [Fact]
        public void DienNuoc_DonGiaKhacNhauTheoKy_NhanDungTheoTungBanGhi()
        {
            var rows = new[]
            {
                Dn(1, 1, 2026, 0, 100, 3000m, 0, 10, 10000m),
                Dn(1, 2, 2026, 100, 150, 4000m, 10, 15, 12000m)
            };
            var d = BaoCaoDienNuocBuilder.Build(PhamVi, Kys(1, 2026, 2, 2026), rows);

            Assert.Equal(100m * 3000m + 50m * 4000m, d.TienDien);
            Assert.Equal(10m * 10000m + 5m * 12000m, d.TienNuoc);
            Assert.Equal(75m, d.TrungBinhKwhPhongThang);
        }

        [Fact]
        public void DienNuoc_ChiSoThapPhan_KhongLamTron()
        {
            var rows = new[] { Dn(1, 1, 2026, 10.125m, 20.375m, 1000m, 1.5m, 2.75m, 2000m) };
            var d = BaoCaoDienNuocBuilder.Build(PhamVi, Kys(1, 2026, 1, 2026), rows);

            Assert.Equal(10.25m, d.TongKwh);
            Assert.Equal(1.25m, d.TongM3);
            Assert.Equal(10250m, d.TienDien);
        }

        [Fact]
        public void DienNuoc_Top10_SapGiamDanTheoKwh_HoaThiTheoSoPhong()
        {
            var rows = Enumerable.Range(1, 12)
                .Select(i => Dn(i, 1, 2026, 0, i <= 2 ? 50 : i * 10, 1000m, 0, 1, 1000m))
                .ToArray();
            var d = BaoCaoDienNuocBuilder.Build(PhamVi, Kys(1, 2026, 1, 2026), rows);

            Assert.Equal(10, d.TopPhong.Count);
            Assert.Equal("P12", d.TopPhong[0].SoPhong);
            // P1 và P2 cùng 50 kWh nhưng P5 cũng 50 kWh: hòa thì theo SoPhong tăng dần.
            var nam = d.TopPhong.Where(p => p.Kwh == 50m).Select(p => p.SoPhong).ToArray();
            Assert.Equal(nam.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray(), nam);
        }

        [Fact]
        public void DienNuoc_KhongCoDuLieu_TrungBinhNull()
        {
            var d = BaoCaoDienNuocBuilder.Build(PhamVi, Kys(1, 2026, 2, 2026), Array.Empty<BaoCaoDienNuocRow>());
            Assert.Null(d.TrungBinhKwhPhongThang);
            Assert.Equal(2, d.TheoKy.Count);
        }
    }
}
