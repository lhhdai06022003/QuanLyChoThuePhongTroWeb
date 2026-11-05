namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoPhamViDto
    {
        public int? ChiNhanhId { get; init; }
        // Tên chi nhánh | "Tất cả chi nhánh" (Admin) | "Tất cả chi nhánh của tôi" (nhân viên).
        public string TenChiNhanh { get; init; } = "";
        public int TuThang { get; init; }
        public int TuNam { get; init; }
        public int DenThang { get; init; }
        public int DenNam { get; init; }
        // "T11/2025"; tab Công nợ để rỗng.
        public string TuKyText { get; init; } = "";
        public string DenKyText { get; init; } = "";
        // Thời điểm tính theo giờ Việt Nam, "dd/MM/yyyy HH:mm".
        public string ThoiDiemTinhText { get; init; } = "";
    }
}
