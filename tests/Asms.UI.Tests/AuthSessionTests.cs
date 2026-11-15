using Asms.Contracts.Auth;
using Asms.UI.Shared.Services;

namespace Asms.UI.Tests;

public sealed class AuthSessionTests
{
    [Fact]
    public void Sign_in_update_and_sign_out_change_session_and_permissions()
    {
        var session = new AuthSession();
        var changedCount = 0;
        session.Changed += () => changedCount++;
        var signedIn = CreateResponse(["Teacher"]);
        var permissions = new[] { "Students.View", "Attendance.View" };

        session.SignIn(signedIn, permissions);

        Assert.True(session.IsSignedIn);
        Assert.Equal(signedIn.UserId, session.UserId);
        Assert.Equal(signedIn.SchoolId, session.SchoolId);
        Assert.Equal(signedIn.AccessToken, session.AccessToken);
        Assert.Equal(signedIn.RefreshToken, session.RefreshToken);
        Assert.Equal(signedIn.ExpiresAtUtc, session.AccessTokenExpiresAtUtc);
        Assert.Equal(["Teacher"], session.Roles);
        Assert.True(session.Has("Students.View"));
        Assert.False(session.Has("Fees.View"));

        var refreshed = signedIn with
        {
            AccessToken = Guid.NewGuid().ToString("N"),
            RefreshToken = Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20)
        };
        session.UpdateTokens(refreshed);

        Assert.Equal(refreshed.AccessToken, session.AccessToken);
        Assert.Equal(refreshed.RefreshToken, session.RefreshToken);
        Assert.Equal(2, changedCount);

        session.SignOut();
        session.SignOut();

        Assert.False(session.IsSignedIn);
        Assert.Null(session.UserId);
        Assert.Null(session.SchoolId);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
        Assert.False(session.Has("Students.View"));
        Assert.Equal(3, changedCount);
    }

    [Fact]
    public void To_string_never_contains_access_or_refresh_tokens()
    {
        var session = new AuthSession();
        var response = CreateResponse([]);

        session.SignIn(response, []);

        var text = session.ToString();
        Assert.DoesNotContain(response.AccessToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(response.RefreshToken, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Concurrent_session_changes_and_reads_do_not_throw()
    {
        var session = new AuthSession();
        var response = CreateResponse(["Teacher"]);

        var exception = Record.Exception(() =>
            Parallel.For(0, 1000, index =>
            {
                if (index % 2 == 0)
                {
                    session.SignIn(response, ["Students.View"]);
                }
                else
                {
                    session.SignOut();
                }

                _ = session.IsSignedIn;
                _ = session.UserId;
                _ = session.SchoolId;
                _ = session.Roles;
                _ = session.Permissions;
                _ = session.AccessToken;
                _ = session.RefreshToken;
                _ = session.AccessTokenExpiresAtUtc;
                _ = session.Has("Students.View");
            }));

        Assert.Null(exception);
    }

    private static LoginResponse CreateResponse(IReadOnlyList<string> roles) =>
        new(
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddMinutes(10),
            Guid.NewGuid(),
            Guid.NewGuid(),
            roles,
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddDays(1));
}
