namespace Asms.UI.Shared.Services;

public sealed class ToastService : IToastService, IDisposable
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);
    private readonly object _sync = new();
    private readonly List<ToastNotification> _notifications = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _dismissals = [];
    private bool _disposed;

    public event Action? Changed;

    public IReadOnlyList<ToastNotification> Notifications
    {
        get
        {
            lock (_sync)
            {
                return _notifications.ToArray();
            }
        }
    }

    public Guid ShowSuccess(string messageKey, TimeSpan? duration = null) =>
        Show(ToastKind.Success, messageKey, duration);

    public Guid ShowError(string messageKey, TimeSpan? duration = null) =>
        Show(ToastKind.Error, messageKey, duration);

    public Guid ShowInfo(string messageKey, TimeSpan? duration = null) =>
        Show(ToastKind.Info, messageKey, duration);

    public void Dismiss(Guid id)
    {
        CancellationTokenSource? timer;
        lock (_sync)
        {
            var index = _notifications.FindIndex(notification => notification.Id == id);
            if (index < 0)
            {
                return;
            }

            _notifications.RemoveAt(index);
            _dismissals.Remove(id, out timer);
        }

        if (timer is not null)
        {
            timer.Cancel();
            timer.Dispose();
        }

        Changed?.Invoke();
    }

    public void Dispose()
    {
        CancellationTokenSource[] timers;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            timers = _dismissals.Values.ToArray();
            _dismissals.Clear();
            _notifications.Clear();
        }

        foreach (var timer in timers)
        {
            timer.Cancel();
            timer.Dispose();
        }
    }

    private Guid Show(ToastKind kind, string messageKey, TimeSpan? duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageKey);
        var lifetime = duration ?? DefaultDuration;
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        var notification = new ToastNotification(Guid.NewGuid(), kind, messageKey);
        var timer = new CancellationTokenSource();
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _notifications.Add(notification);
            _dismissals.Add(notification.Id, timer);
        }

        Changed?.Invoke();
        _ = DismissAfterAsync(notification.Id, lifetime, timer);
        return notification.Id;
    }

    private async Task DismissAfterAsync(
        Guid id,
        TimeSpan duration,
        CancellationTokenSource timer)
    {
        try
        {
            await Task.Delay(duration, timer.Token);
            Dismiss(id);
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested)
        {
        }
    }
}
