using Asms.UI.Shared.Services;

namespace Asms.UI.Tests;

public sealed class ApiOptionsTests
{
    [Fact]
    public void Default_base_url_is_absolute_http()
    {
        var options = new ApiOptions();

        var address = options.CreateBaseAddress();

        Assert.Equal(ApiOptions.DefaultBaseUrl + "/", address.AbsoluteUri);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://example.invalid")]
    [InlineData("https://user:pass@example.invalid")]
    [InlineData("https://example.invalid/path?query=value")]
    public void Invalid_base_urls_are_rejected(string baseUrl)
    {
        var options = new ApiOptions { BaseUrl = baseUrl };

        Assert.Throws<InvalidOperationException>(() => options.CreateBaseAddress());
    }
}
