using System.Net;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class StaticFilesTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public StaticFilesTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task StaticFiles_Favicon_ShouldReturnSuccessStatusCode()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/favicon.ico");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.ContentLength > 0);
        }
    }
}
