namespace Asms.Desktop;

public partial class App : Application
{
	private readonly Asms.Localization.Services.ILocalizer _localizer;

	public App(Asms.Localization.Services.ILocalizer localizer)
	{
		_localizer = localizer;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage())
		{
			Title = _localizer["App.Name"],
			MinimumWidth = 900,
			MinimumHeight = 600
		};
	}
}
