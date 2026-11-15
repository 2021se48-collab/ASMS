namespace Asms.UI.Shared.Services;

public sealed class ApiOptions
{
    public const string ConfigurationKey = "Api:BaseUrl";
    public const string DefaultBaseUrl = "http://localhost:5082";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public Uri CreateBaseAddress()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var address) ||
            (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            throw new InvalidOperationException("Api:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        var builder = new UriBuilder(address);
        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }
}
