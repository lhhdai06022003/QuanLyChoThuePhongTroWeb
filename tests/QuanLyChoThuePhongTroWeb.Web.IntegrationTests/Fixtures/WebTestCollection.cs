using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures
{
    [CollectionDefinition(WebTestCollection.Name, DisableParallelization = true)]
    public class WebTestCollection : ICollectionFixture<CustomWebApplicationFactory>
    {
        public const string Name = "WebIntegrationCollection";
    }
}
