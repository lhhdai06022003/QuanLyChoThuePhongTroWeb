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
                        new MenuItem { Name = "Danh sách", Url = "/User/Index" },
                        new MenuItem { Name = "Thêm mới", Url = "/User/Create" }
                    }
                },
                new MenuItem
                {
                    Name = "Quản lý nhà trọ",
                    Icon = "fas fa-building", // Icon tòa nhà
                    Children = new List<MenuItem>
                    {
                        // Thay /Admin/ChiNhanh/Index nếu bạn đã dùng Area, hoặc /ChiNhanh/Index nếu chưa
                        new MenuItem { Name = "Chi nhánh", Url = "/ChiNhanh/QuanLyChiNhanh" },
                        new MenuItem { Name = "Phòng trọ", Url = "/QuanLy/PhongTro/Index" }
                    }
                }
            };
        }
    }
}
