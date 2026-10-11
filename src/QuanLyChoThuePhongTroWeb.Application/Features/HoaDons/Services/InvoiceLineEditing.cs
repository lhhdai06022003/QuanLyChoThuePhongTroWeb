using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // Một dòng khoản phí do người dùng gửi lên khi sửa hóa đơn. ChiTietHoaDonId rỗng hoặc 0 là dòng mới.
    public sealed record InvoiceLineInput(int? ChiTietHoaDonId, string? TenDichVu, decimal DonGia, decimal SoLuong, int? DichVuId = null);

    // Dòng đang lưu của hóa đơn, chụp trước khi bị sửa để so sánh và giữ thành tiền đã tính theo ngày ở.
    public sealed record InvoiceLineBaseline(int ChiTietHoaDonId, string TenDichVu, decimal DonGia, decimal SoLuong, decimal TongTien, int? DichVuId);

    /// <summary>
    /// Quy tắc chung khi thay toàn bộ dòng khoản phí của hóa đơn (sửa hóa đơn nháp và điều chỉnh hóa đơn tự động).
    /// Dòng cũ không đổi đơn giá và số lượng giữ nguyên thành tiền đã lưu: tiền phòng và dịch vụ cố định có thể đã tính theo số ngày ở
    /// (SoLuong = 1, TongTien &lt; DonGia), tính lại sẽ mất phần đó.
    /// </summary>
    public static class InvoiceLineEditing
    {
        public const int MaxLines = 50;
        public const int MaxNameLength = 500;
        public const decimal MaxLineAmount = 999_999_999_999m;

        public static IReadOnlyDictionary<int, InvoiceLineBaseline> Baseline(IEnumerable<ChiTietHoaDon> lines)
        {
            return lines
                .Where(x => !x.IsDeleted && x.ChiTietHoaDonId > 0)
                .ToDictionary(
                    x => x.ChiTietHoaDonId,
                    x => new InvoiceLineBaseline(x.ChiTietHoaDonId, x.TenDichVu ?? string.Empty, x.DonGia, x.SoLuong, x.TongTien, x.DichVuId));
        }

        // Trả thông báo lỗi đầu tiên, hoặc null nếu hợp lệ. Thứ tự và nội dung ba thông báo đầu giữ nguyên như chức năng sửa hóa đơn nháp.
        public static string? Validate(IReadOnlyList<InvoiceLineInput> lines)
        {
            if (lines.Count > MaxLines)
            {
                return $"Hóa đơn tối đa {MaxLines} dòng khoản phí.";
            }

            foreach (var r in lines)
            {
                if (string.IsNullOrWhiteSpace(r.TenDichVu))
                {
                    return "Tên dịch vụ không được để trống.";
                }

                if (r.DonGia < 0 || r.SoLuong < 0)
                {
                    return "Đơn giá và số lượng phải lớn hơn hoặc bằng 0.";
                }

                if (!InvoiceMoney.IsWholeVnd(r.DonGia))
                {
                    return "Đơn giá phải là số nguyên VND.";
                }

                if (r.TenDichVu.Trim().Length > MaxNameLength)
                {
                    return $"Tên khoản phí không được vượt quá {MaxNameLength} ký tự.";
                }

                if (r.SoLuong.Scale > 3)
                {
                    return "Số lượng không được có quá 3 chữ số thập phân.";
                }

                // Chặn trước khi nhân để không tràn decimal; giới hạn này cũng nằm trong cột decimal(18,2).
                if (r.DonGia > MaxLineAmount || r.SoLuong > MaxLineAmount || InvoiceMoney.RoundVnd(r.DonGia * r.SoLuong) > MaxLineAmount)
                {
                    return "Số tiền của một dòng không được vượt quá 999.999.999.999 đ.";
                }
            }

            return null;
        }

        // Mô tả ngắn thay đổi dòng để ghi vào lịch sử hóa đơn, ví dụ "sửa 2 dòng, thêm 1 dòng, xóa 1 dòng"; null nếu danh sách
        // gửi lên giống hệt các dòng đang lưu (cùng dòng, tên, đơn giá, số lượng). Id lạ hoặc lặp lại tính là dòng thêm.
        public static string? TomTat(IReadOnlyList<InvoiceLineInput> lines, IReadOnlyDictionary<int, InvoiceLineBaseline> baseline)
        {
            var conLai = new Dictionary<int, InvoiceLineBaseline>(baseline);
            var sua = 0;
            var them = 0;

            foreach (var r in lines)
            {
                if (r.ChiTietHoaDonId is int id && id > 0 && conLai.Remove(id, out var cu))
                {
                    if (!string.Equals(cu.TenDichVu.Trim(), (r.TenDichVu ?? string.Empty).Trim(), StringComparison.Ordinal)
                        || cu.DonGia != r.DonGia
                        || cu.SoLuong != r.SoLuong)
                    {
                        sua++;
                    }
                }
                else
                {
                    them++;
                }
            }

            var xoa = conLai.Count;
            var parts = new List<string>();
            if (sua > 0) parts.Add($"sửa {sua} dòng");
            if (them > 0) parts.Add($"thêm {them} dòng");
            if (xoa > 0) parts.Add($"xóa {xoa} dòng");
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        // Dựng danh sách dòng mới (chưa lưu). Phải gọi Validate trước. Mỗi dòng cũ chỉ được khớp một lần.
        // dichVuIdTheoYeuCau: true lấy DichVuId do người dùng gửi (sửa hóa đơn nháp); false lấy theo dòng cũ khớp id, dòng mới không gắn dịch vụ.
        public static List<ChiTietHoaDon> Build(
            int hoaDonId,
            IReadOnlyList<InvoiceLineInput> lines,
            IReadOnlyDictionary<int, InvoiceLineBaseline> baseline,
            bool dichVuIdTheoYeuCau)
        {
            var conLai = new Dictionary<int, InvoiceLineBaseline>(baseline);
            var result = new List<ChiTietHoaDon>(lines.Count);

            foreach (var r in lines)
            {
                InvoiceLineBaseline? cu = null;
                var khop = r.ChiTietHoaDonId is int ctId && ctId > 0 && conLai.Remove(ctId, out cu);

                var tongTien = khop && cu!.DonGia == r.DonGia && cu.SoLuong == r.SoLuong
                    ? InvoiceMoney.RoundVnd(cu.TongTien)
                    : InvoiceMoney.RoundVnd(r.DonGia * r.SoLuong);

                int? dichVuId = dichVuIdTheoYeuCau
                    ? (r.DichVuId > 0 ? r.DichVuId : null)
                    : (khop ? cu!.DichVuId : null);

                result.Add(new ChiTietHoaDon
                {
                    HoaDonId = hoaDonId,
                    TenDichVu = r.TenDichVu!.Trim(),
                    DonGia = r.DonGia,
                    SoLuong = r.SoLuong,
                    TongTien = tongTien,
                    DichVuId = dichVuId
                });
            }

            return result;
        }
    }
}
