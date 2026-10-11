using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public static class InvoiceDetailProjection
    {
        public static bool LaTamTinh(TrangThaiPhatHanhHoaDon trangThai, DateTime? hanPhanHoi, bool isDeleted)
            => trangThai == TrangThaiPhatHanhHoaDon.ChoDuyet && hanPhanHoi != null && !isDeleted;

        // Giờ Việt Nam khi tạm tính, ngược lại chuỗi rỗng.
        public static string HanTuChotText(TrangThaiPhatHanhHoaDon trangThai, DateTime? hanPhanHoi, bool isDeleted)
            => LaTamTinh(trangThai, hanPhanHoi, isDeleted)
                ? hanPhanHoi!.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : string.Empty;

        public static HoaDonChiTietRes ProjectToRes(
            HoaDon hd,
            string donViDien,
            string donViNuoc,
            List<InvoiceStatusHistoryRes>? lichSuTrangThais = null,
            string? lyDoHuy = null)
        {
            var daThu = hd.LichSuThanhToans.Where(x => !x.IsDeleted).Sum(x => x.SoTienThanhToan);
            var ky = hd.DichVuDienNuocCuaPhong;
            var coKy = ky != null && !ky.IsDeleted;
            return new HoaDonChiTietRes
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                TenPhong = hd.HopDong.PhongTro.SoPhong,
                TenNguoiThue = hd.HopDong.NguoiThue.HoVaTen,
                TenChiNhanh = hd.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                DiaChiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.DiaChi,
                SoDienThoaiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.SoDienThoai ?? "",
                Thang = hd.Thang,
                Nam = hd.Nam,
                TongTien = hd.TongTien,
                DaThu = daThu,
                ConLai = hd.TongTien - daThu,
                TrangThaiHoaDon = InvoiceStatusLabels.ThanhToan(hd.TrangThaiHoaDon),
                TrangThaiThanhToanValue = (int)hd.TrangThaiHoaDon,
                ChoPhepThanhToanMotPhan = hd.ChoPhepThanhToanMotPhan,
                SoTienThanhToanToiThieu = hd.SoTienThanhToanToiThieu,
                TrangThaiPhatHanhValue = (int)hd.TrangThaiPhatHanh,
                TrangThaiPhatHanh = InvoiceStatusLabels.PhatHanh(hd.TrangThaiPhatHanh),
                LaHoaDonTuDong = hd.HanPhanHoi != null,
                LaTamTinh = LaTamTinh(hd.TrangThaiPhatHanh, hd.HanPhanHoi, hd.IsDeleted),
                HanTuChot = HanTuChotText(hd.TrangThaiPhatHanh, hd.HanPhanHoi, hd.IsDeleted),
                ChiSoDienCu = coKy ? ky!.ChiSoDienCu : null,
                ChiSoDienMoi = coKy ? ky!.ChiSoDienMoi : null,
                ChiSoNuocCu = coKy ? ky!.ChiSoNuocCu : null,
                ChiSoNuocMoi = coKy ? ky!.ChiSoNuocMoi : null,
                DaHuy = hd.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy,
                LyDoHuy = lyDoHuy ?? string.Empty,
                LichSuTrangThais = lichSuTrangThais ?? new List<InvoiceStatusHistoryRes>(),
                NgayTao = hd.NgayTao.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                HanThanhToan = hd.HanThanhToan.HasValue ? hd.HanThanhToan.Value.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty,
                Email = hd.HopDong.NguoiThue.Email ?? "",
                ChiTietHoaDons = hd.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).Select(ct => new ChiTietHoaDonRes
                {
                    ChiTietHoaDonId = ct.ChiTietHoaDonId,
                    TenDichVu = ct.TenDichVu,
                    DonGia = ct.DonGia,
                    SoLuong = ct.SoLuong,
                    TongTien = ct.TongTien,
                    DonVi = ct.DichVu != null ? ct.DichVu.DonVi :
                            (ct.TenDichVu.Contains("Tiền thuê phòng") ? "Tháng" :
                            (ct.TenDichVu.ToLower().Contains("điện") ? donViDien :
                            (ct.TenDichVu.ToLower().Contains("nước") ? donViNuoc : "")))
                }).ToList(),
                LichSuThanhToans = hd.LichSuThanhToans.Where(x => !x.IsDeleted).Select(ls => new LichSuThanhToanRes
                {
                    LichSuThanhToanId = ls.LichSuThanhToanId,
                    MaGiaoDich = ls.MaGiaoDich,
                    SoTienThanhToan = ls.SoTienThanhToan,
                    PhuongThucThanhToan = ls.PhuongThucThanhToan == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản",
                    NgayThanhToan = ls.NgayThanhToan.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                    NguoiXacNhan = ls.NguoiXacNhan?.TenDangNhap ?? "",
                    GhiChu = PaymentNoteTags.ToDisplayText(ls.GhiChu)
                }).ToList()
            };
        }
    }
}
