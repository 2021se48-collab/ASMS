namespace Asms.UI.Shared.Services;

public enum ToastKind
{
    Success,
    Error,
    Info
}

public sealed record ToastNotification(Guid Id, ToastKind Kind, string MessageKey);

public interface IToastService
{
    IReadOnlyList<ToastNotification> Notifications { get; }

    event Action? Changed;

    Guid ShowSuccess(string messageKey, TimeSpan? duration = null);

    Guid ShowError(string messageKey, TimeSpan? duration = null);

    Guid ShowInfo(string messageKey, TimeSpan? duration = null);

    void Dismiss(Guid id);
}
