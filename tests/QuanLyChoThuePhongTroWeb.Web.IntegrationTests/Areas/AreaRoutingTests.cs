using System.Net;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Areas
{
    [Collection(WebTestCollection.Name)]
    public class AreaRoutingTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public AreaRoutingTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task DangNhapPage_ShouldReturnSuccessStatusCode()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Đăng nhập", content);
        }

        [Fact]
        public async Task ProtectedAdminRoute_WhenUnauthenticated_ShouldRedirectToLogin()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/QuanLyNhaTro/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            Assert.Contains("DangNhap", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task ProtectedTenantRoute_WhenUnauthenticated_ShouldRedirectToLogin()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/KhachThue/LichSuThanhToan");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            Assert.Contains("DangNhap", response.Headers.Location.ToString());
        }
    }
}
