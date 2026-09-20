using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class SignalRTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public SignalRTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SignalR_ThongBaoHub_Negotiate_ShouldReturnSuccess()
        {
            var client = _factory.CreateClient();

            // SignalR negotiate endpoint requires a POST request
            var response = await client.PostAsync("/thongBaoHub/negotiate?negotiateVersion=1", new StringContent(string.Empty));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("connectionId", content);
            Assert.Contains("availableTransports", content);
        }
    }
}
