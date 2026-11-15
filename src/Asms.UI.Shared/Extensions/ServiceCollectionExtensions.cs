using Asms.UI.Shared.Services;
using Microsoft.Extensions.Configuration;
using Asms.Localization.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asms.UI.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsmsUi(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ApiOptions
        {
            BaseUrl = configuration?[ApiOptions.ConfigurationKey] ?? ApiOptions.DefaultBaseUrl
        };
        var baseAddress = options.CreateBaseAddress();

        services.AddSingleton<ILocalizer, Localizer>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAuthSession, AuthSession>();
        services.AddSingleton(options);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddSingleton<ILogger<AsmsApiClient>>(NullLogger<AsmsApiClient>.Instance);
        services.AddSingleton<IAsmsApiClient, AsmsApiClient>();
        services.AddSingleton<ErrorTextService>();
        return services;
    }
}
