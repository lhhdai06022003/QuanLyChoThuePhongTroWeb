using System.Reflection;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    public class WebSmokeTests
    {
        [Fact]
        public void WebAssembly_CanBeLoaded()
        {
            var webAssembly = Assembly.Load("QuanLyChoThuePhongTroWeb.Web");
            Assert.NotNull(webAssembly);
        }
    }
}
