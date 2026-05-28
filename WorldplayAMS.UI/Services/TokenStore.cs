namespace WorldplayAMS.UI.Services;

/// <summary>
/// Simple scoped, in-memory store for the current user's auth token.
/// Shared between the AuthStateProvider (which writes the token) and
/// the TokenDelegatingHandler (which reads it for outbound HTTP calls).
/// This avoids ProtectedSessionStorage JS-interop issues inside DelegatingHandlers.
/// </summary>
public class TokenStore
{
    public string? Token { get; set; }
}
