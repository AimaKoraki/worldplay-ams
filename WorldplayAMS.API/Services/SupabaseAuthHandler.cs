using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace WorldplayAMS.API.Services;

public class SupabaseAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly Supabase.Client _supabaseClient;

    public SupabaseAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        Supabase.Client supabaseClient)
        : base(options, logger, encoder)
    {
        _supabaseClient = supabaseClient;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        if (!AuthenticationHeaderValue.TryParse(Request.Headers["Authorization"], out AuthenticationHeaderValue? headerValue))
            return AuthenticateResult.NoResult();

        if (headerValue.Scheme != "Bearer" || string.IsNullOrWhiteSpace(headerValue.Parameter))
            return AuthenticateResult.NoResult();

        try
        {
            // Verify token with Supabase
            var user = await _supabaseClient.Auth.GetUser(headerValue.Parameter);
            if (user == null)
                return AuthenticateResult.Fail("Invalid Supabase token.");

            // Get user's custom role from database
            var userContext = await _supabaseClient.From<WorldplayAMS.Core.Models.UserContext>()
                .Where(u => u.Id == Guid.Parse(user.Id))
                .Single();

            var role = userContext?.SystemRole ?? "Staff";

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch
        {
            return AuthenticateResult.Fail("Token verification failed.");
        }
    }
}
