using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoCongNoDto
    {
        public BaoCaoPhamViDto PhamVi { get; init; } = new();
        public decimal TongConNo { get; init; }
        public decimal QuaHan { get; init; }
        public decimal NoKhachDaTraPhong { get; init; }
        public int SoHoaDonNo { get; init; }
        public int SoHoaDonQuaHan { get; init; }
        public int SoKhachNo { get; init; }
        // Đủ 6 nhóm theo thứ tự enum NhomTuoiNo.
        public IReadOnlyList<BaoCaoTuoiNoDto> NhomTuoiNo { get; init; } = new List<BaoCaoTuoiNoDto>();
        // Sắp theo TongNo giảm dần.
        public IReadOnlyList<BaoCaoKhachNoDto> TheoKhach { get; init; } = new List<BaoCaoKhachNoDto>();
    }

    public sealed class BaoCaoTuoiNoDto
    {
        public string Ma { get; init; } = "";
        public string Nhan { get; init; } = "";
        public int SoHoaDon { get; init; }
        public decimal SoTien { get; init; }
    }

    public sealed class BaoCaoKhachNoDto
    {
        public int NguoiThueId { get; init; }
        public string HoVaTen { get; init; } = "";
        public string SoPhong { get; init; } = "";
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = "";
        public int SoHoaDon { get; init; }
        public decimal TongNo { get; init; }
        public string KyNoCuNhat { get; init; } = "";
        public int? SoNgayQuaHanLonNhat { get; init; }
        // Quy tắc: true khi MỌI hóa đơn nợ của khách đều thuộc hợp đồng đã trả phòng.
        public bool DaTraPhong { get; init; }
    }
}
