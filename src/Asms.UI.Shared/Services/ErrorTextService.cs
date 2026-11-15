using Asms.Localization.Services;

namespace Asms.UI.Shared.Services;

public sealed class ErrorTextService(ILocalizer localizer)
{
    public string ErrorText(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return localizer["Error.Unexpected"];
        }

        var key = $"Error.{code}";
        var text = localizer[key];
        return text == $"[{key}]" ? localizer["Error.Unexpected"] : text;
    }
}
