using Asms.Localization.Services;
using Asms.UI.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.UI.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsmsUi(this IServiceCollection services)
    {
        services.AddSingleton<ILocalizer, Localizer>();
        services.AddSingleton<IThemeService, ThemeService>();
        return services;
    }
}
