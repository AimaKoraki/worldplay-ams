using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Base class for all WorldplayAMS UI tests.
/// Provides shared helpers for authentication and navigation against the Blazor Server app.
/// 
/// IMPORTANT: The Blazor app must be running locally before executing these tests.
/// Start it with:  dotnet run --project WorldplayAMS.UI --launch-profile https
/// The app should be available at the BaseUrl configured below.
/// </summary>
public abstract class WorldplayTestBase : PageTest
{
    /// <summary>
    /// Base URL of the locally-running Blazor Server app.
    /// Change this if your launchSettings.json uses a different port.
    /// </summary>
    protected const string BaseUrl = "http://localhost:5100";

    /// <summary>
    /// Navigate to a path relative to the base URL.
    /// </summary>
    protected async Task NavigateToAsync(string relativePath = "")
    {
        var url = $"{BaseUrl}/{relativePath.TrimStart('/')}";
        
        // If we are already on the site, use client-side routing to preserve the Blazor SignalR circuit
        // and the Scoped authentication state.
        if (Page.Url.StartsWith(BaseUrl) && Page.Url != url)
        {
            await Page.EvaluateAsync($@"
                const a = document.createElement('a');
                a.href = '{url}';
                document.body.appendChild(a);
                a.click();
                a.remove();
            ");
            // Wait for Blazor to complete the navigation
            await Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(url) + "($|\\?)"), new() { Timeout = 10000 });
            await Task.Delay(500); // Give Blazor a moment to render the new page
        }
        else if (Page.Url != url)
        {
            await Page.GotoAsync(url);
        }
    }

    /// <summary>
    /// Log in using the DEV MODE quick-access role buttons on the login page.
    /// This only works when the app is running in the Development environment.
    /// </summary>
    /// <param name="role">One of: "Admin", "Technician", "Staff"</param>
    protected async Task LoginAsAsync(string role)
    {
        await NavigateToAsync("/login");

        // Wait for the login page to fully render
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });
        
        // IMPORTANT: Blazor Server requires the SignalR connection to establish before interactivity works.
        // Wait for network idle and add a small delay to ensure the buttons are actually clickable.
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        await Task.Delay(1000);

        // Click the appropriate DEV MODE quick-access button
        var roleClass = role.ToLower() switch
        {
            "admin" => ".role-admin",
            "technician" => ".role-tech",
            "staff" => ".role-staff",
            _ => throw new ArgumentException($"Unknown role: {role}. Use Admin, Technician, or Staff.")
        };

        await Page.Locator(roleClass).ClickAsync();

        // Wait for navigation to complete (should redirect to dashboard)
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/$|.*/\?"), new() { Timeout = 10000 });
    }

    /// <summary>
    /// Log in via the standard email/password form.
    /// </summary>
    protected async Task LoginWithCredentialsAsync(string email, string password)
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        await Page.Locator("#input-email").FillAsync(email);
        await Page.Locator("#input-password").FillAsync(password);
        await Page.Locator("#btn-login").ClickAsync();
    }

    /// <summary>
    /// Wait for Blazor Server to establish its SignalR connection and the page content to stabilize.
    /// </summary>
    protected async Task WaitForBlazorAsync()
    {
        // Give Blazor Server a moment to hydrate interactive components
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
    }
}
