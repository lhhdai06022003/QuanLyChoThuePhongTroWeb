namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public class DienNuocPhongRes
    {
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public string TenNguoiDaiDien { get; set; } = string.Empty;
        
        public decimal ChiSoDienCu { get; set; }
        public decimal ChiSoDienMoi { get; set; }
        
        public decimal ChiSoNuocCu { get; set; }
        public decimal ChiSoNuocMoi { get; set; }

        public bool IsDaChot { get; set; } // true if this record already exists in DB for the given month/year
        public bool IsLocked { get; set; }
        public string? LyDoKhoa { get; set; }

        public bool CoAnhDienChinhThuc { get; set; }
        public bool CoAnhNuocChinhThuc { get; set; }
        public decimal? GiaTriDienXacNhanTuAnh { get; set; }
        public decimal? GiaTriNuocXacNhanTuAnh { get; set; }
        public QuanLyChoThuePhongTroWeb.Domain.Enums.TrangThaiGhiNhan? TrangThaiGhiNhan { get; set; }

        public int SoAnhDien { get; set; }
        public int SoAnhNuoc { get; set; }
        public QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiAnhChiSo? TrangThaiAnhDien { get; set; }
        public QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiAnhChiSo? TrangThaiAnhNuoc { get; set; }
    }
}
