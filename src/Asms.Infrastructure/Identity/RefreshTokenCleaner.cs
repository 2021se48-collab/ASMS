using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Identity;

public sealed class RefreshTokenCleaner
{
    private readonly AsmsIdentityDbContext _db;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenCleaner(
        AsmsIdentityDbContext db,
        TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public Task<int> PurgeExpiredAsync(
        TimeSpan keepFor,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keepFor, TimeSpan.Zero);
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - keepFor;
        return _db.RefreshTokens
            .Where(token =>
                token.ExpiresAtUtc < cutoff &&
                token.SessionExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
