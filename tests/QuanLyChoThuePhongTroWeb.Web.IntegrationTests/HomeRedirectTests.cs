using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class HomeRedirectTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Root_Anonymous_OpensPublicRoomListing()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/phong", response.Headers.Location?.ToString());

        var listing = await client.GetAsync("/phong");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
    }
}
