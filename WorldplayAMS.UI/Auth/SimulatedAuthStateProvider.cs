using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WorldplayAMS.UI.Auth;

public class SimulatedAuthStateProvider : AuthenticationStateProvider
{
    private readonly WorldplayAMS.UI.Services.TokenStore _tokenStore;
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    public SimulatedAuthStateProvider(WorldplayAMS.UI.Services.TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    public Task LoginAsAsync(string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, $"Test {role}"),
            new Claim(ClaimTypes.Role, role)
        }, "Simulated Authentication");

        _currentUser = new ClaimsPrincipal(identity);

        // Store the dev token so TokenDelegatingHandler attaches it to API requests
        _tokenStore.Token = "DEV_SIMULATED_TOKEN";

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return Task.CompletedTask;
    }

    public Task LogoutAsync()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _tokenStore.Token = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return Task.CompletedTask;
    }
}
