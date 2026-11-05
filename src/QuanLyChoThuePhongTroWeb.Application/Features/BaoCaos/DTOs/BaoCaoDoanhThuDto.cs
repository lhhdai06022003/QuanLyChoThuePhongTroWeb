using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoDoanhThuDto
    {
        public BaoCaoPhamViDto PhamVi { get; init; } = new();
        public BaoCaoDoanhThuDongDto Tong { get; init; } = new();
        public IReadOnlyList<BaoCaoDoanhThuDongDto> TheoKy { get; init; } = new List<BaoCaoDoanhThuDongDto>();
        public IReadOnlyList<BaoCaoDoanhThuDongDto> TheoChiNhanh { get; init; } = new List<BaoCaoDoanhThuDongDto>();
        // Tiền cọc của hợp đồng đang hoạt động: tiền giữ hộ khách, không phải doanh thu. Không theo kỳ.
        public decimal TienCocDangGiu { get; init; }
        // Tiền giữ chỗ đã nhận nhưng chưa có quyết định hoàn tiền. Không theo kỳ.
        public decimal GiuChoDangChoQuyetDinh { get; init; }
    }

    public sealed class BaoCaoDoanhThuDongDto
    {
        public string Nhan { get; init; } = "";
        public int? Thang { get; init; }
        public int? Nam { get; init; }
        public int? ChiNhanhId { get; init; }
        public int SoHoaDon { get; init; }
        public decimal PhatSinh { get; init; }
        public decimal ThuTien { get; init; }
        public decimal CanTruCoc { get; init; }
        public decimal DaThu { get; init; }
        public decimal ConNo { get; init; }
        public decimal? TyLeThu { get; init; }
        public decimal TienMat { get; init; }
        public decimal ChuyenKhoan { get; init; }
        public decimal SePay { get; init; }
        public decimal VnPay { get; init; }
        public decimal TienThua { get; init; }
        public decimal ThuKhacGiuCho { get; init; }
    }
}
