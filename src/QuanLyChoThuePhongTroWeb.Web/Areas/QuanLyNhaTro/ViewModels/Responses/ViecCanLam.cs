using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class ViecCanLamItemViewModel
    {
        public string MaViec { get; set; } = "";
        public string Nhom { get; set; } = "";         // tên enum NhomViec, dùng cho data-nhom
        public string TenNhom { get; set; } = "";
        public MucUuTienViec MucUuTien { get; set; }
        public string TenMucUuTien { get; set; } = "";
        public string MauUuTien { get; set; } = "";    // red / orange / blue
        public string TieuDe { get; set; } = "";
        public string MoTa { get; set; } = "";
        public int SoLuong { get; set; }
        public string Link { get; set; } = "";
        public string NhanNut { get; set; } = "";

        public static ViecCanLamItemViewModel FromDto(ViecCanLamDto dto)
        {
            return new ViecCanLamItemViewModel
            {
                MaViec = dto.MaViec,
                Nhom = dto.Nhom.ToString(),
                TenNhom = dto.Nhom switch
                {
                    NhomViec.ChiSo => "Chỉ số",
                    NhomViec.HoaDon => "Hóa đơn",
                    NhomViec.ThuTien => "Thu tiền",
                    _ => "Khách thuê & HĐ"
                },
                MucUuTien = dto.MucUuTien,
                TenMucUuTien = dto.MucUuTien switch
                {
                    MucUuTienViec.Khan => "Khẩn",
                    MucUuTienViec.CanLam => "Cần làm",
                    _ => "Theo dõi"
                },
                MauUuTien = dto.MucUuTien switch
                {
                    MucUuTienViec.Khan => "red",
                    MucUuTienViec.CanLam => "orange",
                    _ => "blue"
                },
                TieuDe = dto.TieuDe,
                MoTa = dto.MoTa,
                SoLuong = dto.SoLuong,
                Link = dto.Link,
                NhanNut = dto.MaViec is "TT2" or "TT3" or "KH3" ? "Xem" : "Xử lý"
            };
        }
    }

    public class ViecCanLamKhoiViewModel
    {
        public int TongSo { get; set; }
        public int SoKhan { get; set; }
        public int SoCanLam { get; set; }
        public int SoTheoDoi { get; set; }
        public List<ViecCanLamItemViewModel> Items { get; set; } = new();

        // gioiHan = null: lấy hết. Giữ nguyên thứ tự service trả về.
        public static ViecCanLamKhoiViewModel FromDto(ViecCanLamTongHopDto dto, int? gioiHan = null)
        {
            IEnumerable<ViecCanLamDto> items = dto.Items;
            if (gioiHan.HasValue) items = System.Linq.Enumerable.Take(items, gioiHan.Value);
            return new ViecCanLamKhoiViewModel
            {
                TongSo = dto.TongSo,
                SoKhan = dto.SoKhan,
                SoCanLam = dto.SoCanLam,
                SoTheoDoi = dto.SoTheoDoi,
                Items = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(items, ViecCanLamItemViewModel.FromDto))
            };
        }
    }

    public class HoaDonQuaHanItemViewModel
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = "";
        public string PhongChiNhanh { get; set; } = "";
        public string KhachThue { get; set; } = "";
        public string SoConNoText { get; set; } = "";
        public string HanThanhToanText { get; set; } = "";
        public int SoNgayQuaHan { get; set; }
        public string Link { get; set; } = "";
    }

    public class ViecCanLamPageViewModel
    {
        public int? SelectedChiNhanhId { get; set; }
        // Chi nhánh trên URL không nằm trong danh sách được xem (ngoài phạm vi hoặc đã xóa).
        public bool ChiNhanhNgoaiPhamVi { get; set; }
        public List<SelectListItem> ChiNhanhs { get; set; } = new();
        public ViecCanLamKhoiViewModel ViecCanLam { get; set; } = new();
        public List<HoaDonQuaHanItemViewModel> HoaDonQuaHan { get; set; } = new();
        public int TongSoHoaDonQuaHan { get; set; }
        public int SoHoaDonQuaHanConLai { get; set; }

        public static ViecCanLamPageViewModel From(ViecCanLamTongHopDto tongHop, DanhSachHoaDonQuaHanDto quaHan, IReadOnlyList<SelectOptionDto> chiNhanhs, int? chiNhanhId)
        {
            var selected = chiNhanhId?.ToString(CultureInfo.InvariantCulture);
            var model = new ViecCanLamPageViewModel
            {
                SelectedChiNhanhId = chiNhanhId,
                ViecCanLam = ViecCanLamKhoiViewModel.FromDto(tongHop),
                TongSoHoaDonQuaHan = quaHan.TongSo,
                SoHoaDonQuaHanConLai = System.Math.Max(0, quaHan.TongSo - quaHan.Items.Count)
            };
            model.ChiNhanhNgoaiPhamVi = chiNhanhId.HasValue && !System.Linq.Enumerable.Any(chiNhanhs, c => c.Value == selected);
            foreach (var c in chiNhanhs)
            {
                model.ChiNhanhs.Add(new SelectListItem { Value = c.Value, Text = c.Text, Selected = c.Value == selected });
            }
            foreach (var h in quaHan.Items)
            {
                model.HoaDonQuaHan.Add(new HoaDonQuaHanItemViewModel
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    PhongChiNhanh = $"{h.SoPhong} · {h.TenChiNhanh}",
                    KhachThue = h.KhachThue,
                    SoConNoText = TienTe.DinhDang(h.SoConNo),
                    HanThanhToanText = h.HanThanhToanUtc.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    SoNgayQuaHan = h.SoNgayQuaHan,
                    Link = $"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={h.HoaDonId}"
                });
            }
            return model;
        }
    }
}
