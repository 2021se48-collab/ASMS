using Microsoft.Extensions.Logging;
using Asms.UI.Shared.Extensions;

namespace Asms.Desktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>();

		builder.Services.AddMauiBlazorWebView();
		// development default; configurable per school later
		builder.Configuration["Api:BaseUrl"] = "http://localhost:5082";
		builder.Services.AddAsmsUi(builder.Configuration);

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
