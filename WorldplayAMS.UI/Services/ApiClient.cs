using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WorldplayAMS.UI.Services;

/// <summary>
/// Scoped wrapper around IHttpClientFactory that manually attaches the auth token
/// from the circuit-scoped TokenStore before each request.
/// 
/// This solves the fundamental Blazor Server issue where DelegatingHandlers are resolved
/// in a different DI scope than the Blazor circuit, making them unable to access
/// circuit-scoped services like TokenStore or ProtectedSessionStorage.
/// </summary>
public class ApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly TokenStore _tokenStore;

    public ApiClient(IHttpClientFactory factory, TokenStore tokenStore)
    {
        _factory = factory;
        _tokenStore = tokenStore;
    }

    public HttpClient CreateClient()
    {
        var client = _factory.CreateClient("ApiClient");
        if (!string.IsNullOrWhiteSpace(_tokenStore.Token))
        {
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _tokenStore.Token);
        }
        return client;
    }
}
