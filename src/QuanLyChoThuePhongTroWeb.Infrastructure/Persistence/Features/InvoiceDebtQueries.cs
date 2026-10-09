using System;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    // Định nghĩa dùng chung cho Dashboard và Việc cần làm: công nợ chỉ tính hóa đơn đã gửi khách.
    internal static class InvoiceDebtQueries
    {
        // Hóa đơn đã gửi khách, chưa xóa, chưa thanh toán đủ (khớp màn Công nợ).
        public static IQueryable<HoaDon> DaGuiChuaThuDu(this IQueryable<HoaDon> q) =>
            q.Where(h => !h.IsDeleted
                         && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui
                         && h.TrangThaiHoaDon != TrangThaiHoaDon.DaThanhToan);

        public static IQueryable<HoaDon> QuaHan(this IQueryable<HoaDon> q, DateTime nowUtc) =>
            q.DaGuiChuaThuDu().Where(h => h.HanThanhToan != null && h.HanThanhToan < nowUtc);

        public static IQueryable<HoaDon> ChuaToiHan(this IQueryable<HoaDon> q, DateTime nowUtc) =>
            q.DaGuiChuaThuDu().Where(h => h.HanThanhToan == null || h.HanThanhToan >= nowUtc);
    }
}
