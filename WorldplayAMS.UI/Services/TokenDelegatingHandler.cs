using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace WorldplayAMS.UI.Services;

public class TokenDelegatingHandler : DelegatingHandler
{
    private readonly ProtectedSessionStorage _sessionStorage;

    public TokenDelegatingHandler(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sessionStorage.GetAsync<WorldplayAMS.UI.Auth.SupabaseAuthStateProvider.AuthUserData>("auth_user");
            if (result.Success && result.Value != null && !string.IsNullOrWhiteSpace(result.Value.Token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.Value.Token);
            }
        }
        catch 
        { 
            // Ignore JS interop errors during prerendering
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
