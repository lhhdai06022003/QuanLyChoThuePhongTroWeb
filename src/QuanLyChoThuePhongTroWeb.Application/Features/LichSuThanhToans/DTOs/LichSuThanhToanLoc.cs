using System;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs
{
    // Tham số thô từ Web (chưa kiểm).
    public sealed class LichSuThanhToanLocReq
    {
        public int ChiNhanhId { get; init; }          // 0 = mọi chi nhánh trong phạm vi
        public string? PhuongThuc { get; init; }      // slug kênh thu
        public DateTime? TuNgay { get; init; }        // ngày lịch VN
        public DateTime? DenNgay { get; init; }
        public bool ChiCoTienThua { get; init; }
        public string? NguoiThu { get; init; }        // "toi", "he-thong", NguoiDungId, hoặc rỗng = tất cả
        public int? KyThang { get; init; }            // kỳ hóa đơn; chỉ Admin được áp dụng
        public int? KyNam { get; init; }
    }

    public enum NguoiThuLoai
    {
        TatCa,
        NguoiDung,
        HeThong
    }

    // Bộ lọc đã chuẩn hóa, store nhận.
    public sealed class LichSuThanhToanLoc
    {
        public int ChiNhanhId { get; init; }
        public KenhThu? Kenh { get; init; }           // null = mọi kênh
        public DateTime? TuNgay { get; init; }
        public DateTime? DenNgay { get; init; }
        public bool ChiCoTienThua { get; init; }
        public NguoiThuLoai NguoiThu { get; init; }
        public int? NguoiThuId { get; init; }         // có giá trị khi NguoiThu = NguoiDung
        // Cả hai có giá trị = lọc theo kỳ hóa đơn (hóa đơn DaGui, chưa xóa); khi đó bỏ qua TuNgay/DenNgay.
        public int? KyThang { get; init; }
        public int? KyNam { get; init; }
    }
}
