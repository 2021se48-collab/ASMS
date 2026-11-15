using System.Collections.Frozen;
using Asms.Contracts.Auth;

namespace Asms.UI.Shared.Services;

public sealed class AuthSession : IAuthSession
{
    private readonly object _gate = new();
    private bool _isSignedIn;
    private Guid? _userId;
    private Guid? _schoolId;
    private string[] _roles = [];
    private FrozenSet<string> _permissions = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);
    // Tokens stay in memory; secure storage comes later.
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _accessTokenExpiresAtUtc;

    public event Action? Changed;

    public bool IsSignedIn
    {
        get
        {
            lock (_gate)
            {
                return _isSignedIn;
            }
        }
    }

    public Guid? UserId
    {
        get
        {
            lock (_gate)
            {
                return _userId;
            }
        }
    }

    public Guid? SchoolId
    {
        get
        {
            lock (_gate)
            {
                return _schoolId;
            }
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(_roles.ToArray());
            }
        }
    }

    public IReadOnlySet<string> Permissions
    {
        get
        {
            lock (_gate)
            {
                return _permissions;
            }
        }
    }

    public string? AccessToken
    {
        get
        {
            lock (_gate)
            {
                return _accessToken;
            }
        }
    }

    public string? RefreshToken
    {
        get
        {
            lock (_gate)
            {
                return _refreshToken;
            }
        }
    }

    public DateTime? AccessTokenExpiresAtUtc
    {
        get
        {
            lock (_gate)
            {
                return _accessTokenExpiresAtUtc;
            }
        }
    }

    public void SignIn(LoginResponse response, IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(permissions);

        Action? handlers;
        lock (_gate)
        {
            SetIdentity(response);
            _permissions = permissions.ToFrozenSet(StringComparer.Ordinal);
            _isSignedIn = true;
            handlers = Changed;
        }

        handlers?.Invoke();
    }

    public void UpdateTokens(LoginResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        Action? handlers;
        lock (_gate)
        {
            if (!_isSignedIn)
            {
                return;
            }

            SetIdentity(response);
            handlers = Changed;
        }

        handlers?.Invoke();
    }

    public void SignOut()
    {
        Action? handlers;
        lock (_gate)
        {
            if (!_isSignedIn && _accessToken is null && _refreshToken is null)
            {
                return;
            }

            _isSignedIn = false;
            _userId = null;
            _schoolId = null;
            _roles = [];
            _permissions = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);
            _accessToken = null;
            _refreshToken = null;
            _accessTokenExpiresAtUtc = null;
            handlers = Changed;
        }

        handlers?.Invoke();
    }

    public bool Has(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        lock (_gate)
        {
            return _isSignedIn && _permissions.Contains(permission);
        }
    }

    public override string ToString() => $"AuthSession {{ IsSignedIn = {IsSignedIn} }}";

    private void SetIdentity(LoginResponse response)
    {
        _userId = response.UserId;
        _schoolId = response.SchoolId;
        _roles = response.Roles.ToArray();
        _accessToken = response.AccessToken;
        _refreshToken = response.RefreshToken;
        _accessTokenExpiresAtUtc = response.ExpiresAtUtc;
    }
}
