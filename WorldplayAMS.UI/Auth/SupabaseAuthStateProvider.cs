using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;
using System.Net.Http.Json;

namespace WorldplayAMS.UI.Auth;

/// <summary>
/// Production auth provider that authenticates against Supabase Auth via the API proxy.
/// Persists auth state in ProtectedSessionStorage so it survives SignalR reconnects.
/// </summary>
public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly WorldplayAMS.UI.Services.ApiClient _apiClient;
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly WorldplayAMS.UI.Services.TokenStore _tokenStore;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private bool _initialized = false;

    public SupabaseAuthStateProvider(WorldplayAMS.UI.Services.ApiClient apiClient, ProtectedSessionStorage sessionStorage, WorldplayAMS.UI.Services.TokenStore tokenStore)
    {
        _apiClient = apiClient;
        _sessionStorage = sessionStorage;
        _tokenStore = tokenStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            try
            {
                var result = await _sessionStorage.GetAsync<AuthUserData>("auth_user");
                if (result.Success && result.Value != null)
                {
                    _currentUser = BuildClaimsPrincipal(result.Value);
                    _tokenStore.Token = result.Value.Token;
                }
            }
            catch
            {
                // ProtectedSessionStorage may throw during prerendering — ignore
            }
        }

        return new AuthenticationState(_currentUser);
    }

    /// <summary>
    /// Attempts login via API proxy → Supabase Auth.
    /// Returns null on success, or an error message string on failure.
    /// </summary>
    public async Task<string?> LoginAsync(string email, string password)
    {
        var client = _apiClient.CreateClient();
        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthUserData>();
                if (data != null && data.Authenticated)
                {
                    _currentUser = BuildClaimsPrincipal(data);
                    _tokenStore.Token = data.Token;

                    try
                    {
                        await _sessionStorage.SetAsync("auth_user", data);
                    }
                    catch { /* prerender guard */ }

                    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
                    return null; // success
                }
            }

            // Try to read error message from API
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                return error?.Error ?? "Invalid credentials.";
            }
            catch
            {
                return "Invalid email or password.";
            }
        }
        catch (Exception ex)
        {
            return $"Connection error: {ex.Message}";
        }
    }

    public async Task LogoutAsync()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _tokenStore.Token = null;

        try
        {
            await _sessionStorage.DeleteAsync("auth_user");
        }
        catch { /* prerender guard */ }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(AuthUserData data)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, data.Name),
            new Claim(ClaimTypes.Email, data.Email),
            new Claim(ClaimTypes.Role, data.Role)
        }, "Supabase Authentication");

        return new ClaimsPrincipal(identity);
    }

    public class AuthUserData
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff";
        public bool Authenticated { get; set; }
        public string Token { get; set; } = string.Empty;
    }

    private class ErrorResponse
    {
        public string? Error { get; set; }
    }
}
