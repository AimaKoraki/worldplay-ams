using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Navigation Menu (sidebar).
/// Verifies correct links are shown/hidden based on user role,
/// brand text, and sign-out functionality.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class NavigationTests : WorldplayTestBase
{
    [Test]
    public async Task Navigation_ShouldShowBrandText()
    {
        await LoginAsAsync("Admin");

        var brand = Page.GetByText("Worldplay");
        await Expect(brand.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_ShouldShowSystemOnlineStatus()
    {
        await LoginAsAsync("Admin");

        var status = Page.GetByText("System Online");
        await Expect(status).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_ShouldShowSignOutButton()
    {
        await LoginAsAsync("Admin");

        var signOut = Page.Locator(".btn-logout");
        await Expect(signOut).ToBeVisibleAsync();
        await Expect(signOut).ToContainTextAsync("Sign Out");
    }

    [Test]
    public async Task Navigation_SignOut_ShouldRedirectToLogin()
    {
        await LoginAsAsync("Admin");

        await Page.Locator(".btn-logout").ClickAsync();

        // Should redirect to login page
        await Page.WaitForURLAsync(new Regex(@".*/login"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/login"));
    }

    [Test]
    public async Task Navigation_AdminRole_ShouldShowAllAdminLinks()
    {
        await LoginAsAsync("Admin");
        var nav = Page.Locator("nav");

        // Admin should see all navigation links
        await Expect(nav.Locator("a").Filter(new() { HasText = "Dashboard" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Check-In Scanner" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Session History" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Transaction History" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Machine Management" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Staff Management" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Data Export" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Diagnostics" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_StaffRole_ShouldShowLimitedLinks()
    {
        await LoginAsAsync("Staff");
        var nav = Page.Locator("nav");

        // Staff should see these
        await Expect(nav.Locator("a").Filter(new() { HasText = "Dashboard" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Check-In Scanner" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Session History" })).ToBeVisibleAsync();

        // Staff should NOT see admin-only links
        await Expect(nav.Locator("a").Filter(new() { HasText = "Machine Management" })).Not.ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Staff Management" })).Not.ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Transaction History" })).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_TechnicianRole_ShouldShowDiagnosticsLink()
    {
        await LoginAsAsync("Technician");
        var nav = Page.Locator("nav");

        // Technician should see these
        await Expect(nav.Locator("a").Filter(new() { HasText = "Dashboard" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Diagnostics" })).ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Asset Logs" })).ToBeVisibleAsync();

        // Technician should NOT see admin-only links
        await Expect(nav.Locator("a").Filter(new() { HasText = "Machine Management" })).Not.ToBeVisibleAsync();
        await Expect(nav.Locator("a").Filter(new() { HasText = "Staff Management" })).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_DashboardLink_ShouldNavigateToHome()
    {
        await LoginAsAsync("Admin");

        // Navigate away first
        await NavigateToAsync("/login");

        // Then use nav to go back via dashboard link
        await LoginAsAsync("Admin");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
    }
}
