using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Filters;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Chống sót bộ lọc ép đổi mật khẩu (spec bảo mật xác thực §2.2, §6): mọi controller của cổng quản lý và cổng khách thuê
    // phải có YeuCauDoiMatKhauAttribute (gắn trực tiếp hoặc kế thừa). Thêm ngoại lệ thì ghi tên controller kèm lý do.
    public class ForcedPasswordChangeArchitectureTests
    {
        private static readonly string[] PortalAreas = { "QuanLyNhaTro", "KhachThue" };

        private static readonly HashSet<string> Exceptions = new(StringComparer.Ordinal)
        {
        };

        private static IReadOnlyList<Type> PortalControllers() =>
            typeof(Program).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttributes<AreaAttribute>(inherit: true).Any(a => PortalAreas.Contains(a.RouteValue)))
                .ToList();

        [Fact]
        public void PortalControllers_AreFound()
        {
            var controllers = PortalControllers();

            Assert.Contains(controllers, t => t.Name == "DashboardController" && t.Namespace!.Contains("QuanLyNhaTro"));
            Assert.Contains(controllers, t => t.Name == "HoSoController" && t.Namespace!.Contains("KhachThue"));
            Assert.Contains(controllers, t => t.Name == "ThongBaoController" && t.Namespace!.Contains("QuanLyNhaTro"));
        }

        [Fact]
        public void EveryPortalController_HasForcedPasswordChangeFilter()
        {
            var missing = PortalControllers()
                .Where(t => !Exceptions.Contains(t.FullName!))
                .Where(t => !t.GetCustomAttributes<YeuCauDoiMatKhauAttribute>(inherit: true).Any())
                .Select(t => t.FullName)
                .ToList();

            Assert.True(missing.Count == 0, "Controller thiếu [YeuCauDoiMatKhau]: " + string.Join(", ", missing));
        }

        [Fact]
        public void ExceptionList_IsEmpty()
        {
            Assert.Empty(Exceptions);
        }
    }
}
