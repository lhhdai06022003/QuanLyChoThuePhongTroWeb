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
                Name = "Quản lý câu hỏi",
                Icon = "fas fa-question",
                Children = new List<MenuItem>
                {
                    new MenuItem { Name = "Chủ đề", Url = "/Topic/Index" },
                    new MenuItem { Name = "Câu hỏi", Url = "/Question/Index" }
                }
            }
        };
        }
    }
}
