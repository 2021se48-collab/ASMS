using Asms.Localization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.UI.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsmsUi(this IServiceCollection services)
    {
        services.AddSingleton<ILocalizer, Localizer>();
        return services;
    }
}
