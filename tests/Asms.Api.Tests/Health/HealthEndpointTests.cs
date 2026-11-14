using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Asms.Api.Tests.Health;

public class HealthEndpointTests
{
    [Fact]
    public async Task Health_endpoint_returns_ok_without_version_details()
    {
        await using var factory = CreateConfiguredFactory();
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
        await using var factory = CreateConfiguredFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateConfiguredFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:Default"] = new SqliteConnectionStringBuilder
                    {
                        DataSource = Guid.NewGuid().ToString("N"),
                        Mode = SqliteOpenMode.Memory,
                        Cache = SqliteCacheMode.Shared
                    }.ToString(),
                    ["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
                }));
        });
}
