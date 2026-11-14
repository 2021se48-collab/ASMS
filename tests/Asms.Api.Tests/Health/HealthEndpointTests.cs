using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Asms.Api.Tests.Health;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_endpoint_returns_ok_without_version_details()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var properties = document.RootElement.EnumerateObject().ToArray();

        Assert.Single(properties);
        Assert.Equal("status", properties[0].Name);
        Assert.Equal("ok", properties[0].Value.GetString());
        Assert.DoesNotContain("version", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("server", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_route_returns_not_found()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
