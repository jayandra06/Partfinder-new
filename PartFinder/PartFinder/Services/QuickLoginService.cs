namespace PartFinder.Services;

public enum QuickLoginResultKind
{
    NoSavedSession,
    Success,
    InvalidSession,
    NetworkError,
}

public sealed class QuickLoginResult
{
    public QuickLoginResultKind Kind { get; init; }
    public string? ErrorDetail { get; init; }
    public string? OrgCode { get; init; }
    public string? Email { get; init; }
}

/// <summary>Restores an invite-login session using a saved refresh token.</summary>
public sealed class QuickLoginService
{
    private readonly SavedLoginCredentialStore _savedLogin;
    private readonly AdminSessionStore _adminSession;

    public QuickLoginService(
        SavedLoginCredentialStore savedLogin,
        AdminSessionStore adminSession)
    {
        _savedLogin = savedLogin;
        _adminSession = adminSession;
    }

    public async Task<QuickLoginResult> TryQuickLoginAsync(CancellationToken ct = default)
    {
        var profile = _savedLogin.LoadProfile();
        if (profile?.OrgCode is null || profile.Email is null)
        {
            return new QuickLoginResult { Kind = QuickLoginResultKind.NoSavedSession };
        }

        var refreshToken = _savedLogin.TryReadRefreshToken(profile);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _savedLogin.Clear();
            return new QuickLoginResult { Kind = QuickLoginResultKind.NoSavedSession };
        }

        var deviceId = DeviceIdentityService.GetDeviceId();
        var (ok, err, body) = await SetupApiClient.RefreshSessionAsync(
            profile.OrgCode,
            profile.Email,
            deviceId,
            refreshToken,
            ct).ConfigureAwait(false);

        if (ok && body?.AccessToken is not null && body.RefreshToken is not null)
        {
            _adminSession.Save(body.AccessToken, profile.Email);
            _savedLogin.ReplaceRefreshToken(profile.OrgCode, profile.Email, body.RefreshToken);

            return new QuickLoginResult
            {
                Kind = QuickLoginResultKind.Success,
                OrgCode = profile.OrgCode,
                Email = profile.Email,
            };
        }

        if (SetupApiClient.LooksLikeNetworkError(err))
        {
            return new QuickLoginResult
            {
                Kind = QuickLoginResultKind.NetworkError,
                ErrorDetail = err,
                OrgCode = profile.OrgCode,
                Email = profile.Email,
            };
        }

        _savedLogin.Clear();
        return new QuickLoginResult
        {
            Kind = QuickLoginResultKind.InvalidSession,
            ErrorDetail = err,
        };
    }
}