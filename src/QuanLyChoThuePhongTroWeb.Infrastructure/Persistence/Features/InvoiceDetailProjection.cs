using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public static class InvoiceDetailProjection
    {
        // Hạn phản hồi chỉ có nghĩa với hóa đơn đã gửi; hóa đơn cũ ở trạng thái khác có thể còn HanPhanHoi nhưng không hiển thị.
        // Hóa đơn cũ do job tự chốt gửi đi có HanPhanHoi (hạn xem xét) sớm hơn NgayGui: không phải hạn phản hồi của khách nên ẩn.
        public static DateTime? HanPhanHoiHienThi(TrangThaiPhatHanhHoaDon trangThai, DateTime? hanPhanHoi, DateTime? ngayGui, bool isDeleted)
            => trangThai == TrangThaiPhatHanhHoaDon.DaGui && !isDeleted && hanPhanHoi.HasValue && ngayGui.HasValue && hanPhanHoi.Value > ngayGui.Value
                ? hanPhanHoi
                : null;

        public static string DinhDangHanPhanHoi(DateTime? hanPhanHoiUtc)
            => hanPhanHoiUtc.HasValue
                ? hanPhanHoiUtc.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : string.Empty;

        // Hóa đơn tự động = có dòng lịch sử mang thẻ [TU_DONG], không suy ra từ HanPhanHoi.
        public static bool LaHoaDonTuDong(IEnumerable<string?> lyDoLichSu)
            => lyDoLichSu.Any(l => l != null && l.StartsWith(AutoInvoiceHistoryTags.TuDong, StringComparison.Ordinal));

        public static HoaDonChiTietRes ProjectToRes(
            HoaDon hd,
            string donViDien,
            string donViNuoc,
            List<InvoiceStatusHistoryRes>? lichSuTrangThais = null,
            string? lyDoHuy = null,
            bool laHoaDonTuDong = false)
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
                LaHoaDonTuDong = laHoaDonTuDong,
                HanPhanHoiUtc = HanPhanHoiHienThi(hd.TrangThaiPhatHanh, hd.HanPhanHoi, hd.NgayGui, hd.IsDeleted),
                HanPhanHoiHienThi = DinhDangHanPhanHoi(HanPhanHoiHienThi(hd.TrangThaiPhatHanh, hd.HanPhanHoi, hd.NgayGui, hd.IsDeleted)),
                ChiSoDienCu = coKy ? ky!.ChiSoDienCu : null,
                ChiSoDienMoi = coKy ? ky!.ChiSoDienMoi : null,
                ChiSoNuocCu = coKy ? ky!.ChiSoNuocCu : null,
                ChiSoNuocMoi = coKy ? ky!.ChiSoNuocMoi : null,
                DaHuy = hd.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy,
                LyDoHuy = AutoInvoiceHistoryTags.BoNhan(lyDoHuy),
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
                    GhiChu = PaymentNoteTags.ToDisplayText(ls.GhiChu),
                    LaSePay = InvoicePaymentPolicy.IsSePayKey(ls.MaGiaoDich),
                    MaThamChieu = InvoicePaymentPolicy.SePayBankReference(ls.MaGiaoDich)
                }).ToList()
            };
        }
    }
}
