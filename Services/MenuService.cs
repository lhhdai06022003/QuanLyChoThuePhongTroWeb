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
                    Icon = "fas fa-home",
                    Url = "/Home/Index"
                },
                new MenuItem
                {
                    Name = "Quản lý người dùng",
                    Icon = "fas fa-users",
                    Children = new List<MenuItem>
                    {
                        //new MenuItem { Name = "Đăng nhập", Url = "/QuanLyNhaTro/DangNhap" },
                        new MenuItem { Name = "Quản lý tài khoản", Url = "/QuanLyNhaTro/QuanLyTaiKhoanDangNhap" }
                    }
                },
                new MenuItem
                {
                    Name = "Quản lý nhà trọ",
                    Icon = "fas fa-building", // Icon tòa nhà
                    Children = new List<MenuItem>
                    {
                        // Thay /Admin/ChiNhanh/Index nếu bạn đã dùng Area, hoặc /ChiNhanh/Index nếu chưa
                        new MenuItem { Name = "Chi nhánh", Url = "/ChiNhanhs/QuanLyChiNhanh" },
                        new MenuItem { Name = "Phòng trọ", Url = "/PhongTros/QuanLyPhongTro" },
                        new MenuItem { Name = "Người thuê", Url = "/QuanLyNhaTro/QuanLyNguoiThue" },
                        new MenuItem { Name = "Hợp đồng", Url = "/QuanLyNhaTro/QuanLyHopDong" },
                        new MenuItem { Name = "Dịch vụ", Url = "/QuanLyNhaTro/QuanLyDichVu" },
                        new MenuItem { Name = "Điện nước", Url = "/QuanLyNhaTro/ChotDienNuoc" },
                        new MenuItem { Name = "Hóa đơn", Url = "/QuanLyNhaTro/QuanLyHoaDon" },
                        new MenuItem { Name = "Lịch sử thanh toán", Url = "/QuanLyNhaTro/LichSuThanhToan" },
                    }
                },
            };
        }
    }
}
