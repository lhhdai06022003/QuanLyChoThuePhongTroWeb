using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs
{
    // SoLuongGap chỉ dùng cho KH1, KH3; các việc khác để 0.
    public sealed class DemChiNhanhRow
    {
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int SoLuong { get; init; }
        public int SoLuongGap { get; init; }
    }

    public sealed class DemTheoKyRow
    {
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
        public int SoLuong { get; init; }
    }

    // TongConNo chỉ dùng cho TT2, TT3; HD1-HD3 để 0.
    public sealed class DemHoaDonRow
    {
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int SoLuong { get; init; }
        public int NamCuNhat { get; init; }
        public decimal TongConNo { get; init; }
    }

    public sealed class HopDongChoChotChiSoRow
    {
        public int PhongTroId { get; init; }
        public DateTime ThoiDiemBatDau { get; init; }
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
    }

    public sealed class KyChiSoDaChotRow
    {
        public int PhongTroId { get; init; }
        public int Thang { get; init; }
        public int Nam { get; init; }
    }

    // HD5: kỳ chỉ số đã chốt mà phòng chưa có hóa đơn nào trong kỳ; ứng viên để đánh giá lại chuỗi tự tạo hóa đơn.
    public sealed class KyDaChotChuaCoHoaDonRow
    {
        public int PhongTroId { get; init; }
        public string SoPhong { get; init; } = string.Empty;
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
    }

    // Kết quả của các truy vấn gộp (docs/features/toi-uu-hieu-nang, bước 16): một lệnh SQL trả số đếm cho nhiều mục cùng bảng,
    // store tách sẵn theo mục. Danh sách nào cũng chỉ chứa chi nhánh có SoLuong > 0, như khi mỗi mục là một truy vấn riêng.
    public sealed class DemHoaDonChuaGuiRows
    {
        public IReadOnlyList<DemHoaDonRow> Nhap { get; init; } = Array.Empty<DemHoaDonRow>();          // HD1
        public IReadOnlyList<DemHoaDonRow> ChoDuyet { get; init; } = Array.Empty<DemHoaDonRow>();      // HD2
        public IReadOnlyList<DemHoaDonRow> DaChotChuaGui { get; init; } = Array.Empty<DemHoaDonRow>(); // HD3
    }

    public sealed class DemHoaDonDaGuiChuaThuDuRows
    {
        public IReadOnlyList<DemHoaDonRow> QuaHan { get; init; } = Array.Empty<DemHoaDonRow>();     // TT2
        public IReadOnlyList<DemHoaDonRow> ChuaToiHan { get; init; } = Array.Empty<DemHoaDonRow>(); // TT3
    }

    public sealed class DemSuCoCanXuLyRows
    {
        public IReadOnlyList<DemChiNhanhRow> ChoTiepNhan { get; init; } = Array.Empty<DemChiNhanhRow>(); // KH1
        public IReadOnlyList<DemChiNhanhRow> XuLyQuaLau { get; init; } = Array.Empty<DemChiNhanhRow>();  // KH2
    }

    public sealed class DemHopDongHetHanRows
    {
        public IReadOnlyList<DemChiNhanhRow> SapHetHan { get; init; } = Array.Empty<DemChiNhanhRow>(); // KH3
        public IReadOnlyList<DemChiNhanhRow> QuaHan { get; init; } = Array.Empty<DemChiNhanhRow>();    // KH4
    }

    public sealed class HoaDonQuaHanRow
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public string KhachThue { get; init; } = string.Empty;
        public decimal TongTien { get; init; }
        public decimal DaThu { get; init; }
        public DateTime HanThanhToan { get; init; }
    }
}
