using QuanLyChoThuePhongTroWeb.Models.ModelsOther;

namespace QuanLyChoThuePhongTroWeb.Services
{
    public class MenuService
    {
        public static List<MenuItem> GetMenu()
        {
            return new List<MenuItem>
            {
                new MenuItem
                {
                    Name = "Dashboard",
                    Icon = "ti ti-layout-dashboard",
                    Url = "/Home/Index"
                },
                new MenuItem
                {
                    Name = "Quản lý người dùng",
                    Icon = "ti ti-user-cog",
                    Children = new List<MenuItem>
                    {
                        new MenuItem { Name = "Quản lý tài khoản", Icon = "ti ti-users-group", Url = "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap" }
                    }
                },
                new MenuItem
                {
                    Name = "Quản lý nhà trọ",
                    Icon = "ti ti-building-community",
                    Children = new List<MenuItem>
                    {
                        new MenuItem { Name = "Chi nhánh", Icon = "ti ti-building-store", Url = "/ChiNhanhs/QuanLyChiNhanh" },
                        new MenuItem { Name = "Phòng trọ", Icon = "ti ti-bed", Url = "/PhongTros/QuanLyPhongTro" },
                        //new MenuItem { Name = "Sơ đồ phòng", Icon = "ti ti-layout-grid", Url = "/PhongTros/SoDoPhong" },
                        new MenuItem { Name = "Người thuê", Icon = "ti ti-users", Url = "/QuanLyNhaTro/QuanLyNguoiThue" },
                        new MenuItem { Name = "Hợp đồng", Icon = "ti ti-file-text", Url = "/QuanLyNhaTro/QuanLyHopDong" },
                        new MenuItem { Name = "Điều khoản hợp đồng", Icon = "ti ti-gavel", Url = "/QuanLyNhaTro/DieuKhoanMau" },
                        new MenuItem { Name = "Dịch vụ", Icon = "ti ti-apps", Url = "/QuanLyNhaTro/QuanLyDichVu" },
                        new MenuItem { Name = "Điện nước", Icon = "ti ti-bolt", Url = "/QuanLyNhaTro/ChotDienNuoc" },
                        new MenuItem { Name = "Hóa đơn", Icon = "ti ti-file-invoice", Url = "/QuanLyNhaTro/QuanLyHoaDon" },
                        new MenuItem { Name = "Lịch sử thanh toán", Icon = "ti ti-history", Url = "/QuanLyNhaTro/LichSuThanhToan" },
                        new MenuItem { Name = "Sự cố & Sửa chữa", Icon = "ti ti-tools", Url = "/QuanLyNhaTro/YeuCauSuCo" }
                    }
                },
            };
        }
    }
}
