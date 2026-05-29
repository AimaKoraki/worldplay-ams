using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for authentication guards and redirect behavior.
/// Verifies that unauthenticated users are redirected to /login,
/// and that authenticated users can access authorized routes.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AuthGuardTests : WorldplayTestBase
{
    [Test]
    public async Task UnauthenticatedUser_ShouldBeRedirectedToLogin_FromDashboard()
    {
        // Try to access dashboard without logging in
        await NavigateToAsync("/");

        // Should be redirected to login page
        await Page.WaitForURLAsync(new Regex(@".*/login"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/login"));
    }

    [Test]
    public async Task UnauthenticatedUser_ShouldBeRedirectedToLogin_FromAdminPages()
    {
        await NavigateToAsync("/admin/machines");

        await Page.WaitForURLAsync(new Regex(@".*/login"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/login"));
    }

    [Test]
    public async Task UnauthenticatedUser_ShouldBeRedirectedToLogin_FromExportPage()
    {
        await NavigateToAsync("/export");

        await Page.WaitForURLAsync(new Regex(@".*/login"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/login"));
    }

    [Test]
    public async Task UnauthenticatedUser_ShouldBeRedirectedToLogin_FromSessionHistory()
    {
        await NavigateToAsync("/sessions");

        await Page.WaitForURLAsync(new Regex(@".*/login"), new() { Timeout = 10000 });
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/login"));
    }

    [Test]
    public async Task UnauthenticatedUser_CanAccessLoginPage()
    {
        await NavigateToAsync("/login");

        // Should stay on login page, not redirect
        await Expect(Page).ToHaveURLAsync(new Regex(@".*/login"));
        await Expect(Page.GetByText("Staff Portal")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AuthenticatedAdmin_CanAccessDashboard()
    {
        await LoginAsAsync("Admin");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
        await Expect(Page.Locator(".dash-title")).ToContainTextAsync("Control Center");
    }

    [Test]
    public async Task AuthenticatedAdmin_CanAccessMachineManagement()
    {
        await LoginAsAsync("Admin");
        await NavigateToAsync("/admin/machines");
        await WaitForBlazorAsync();

        // Should NOT show access denied
        await Expect(Page.Locator("#btn-add-machine")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AuthenticatedAdmin_CanAccessStaffManagement()
    {
        await LoginAsAsync("Admin");
        await NavigateToAsync("/admin/staff");
        await WaitForBlazorAsync();

        await Expect(Page.Locator("#btn-add-staff")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AuthenticatedStaff_CanAccessDashboard()
    {
        await LoginAsAsync("Staff");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
        await Expect(Page.Locator(".dash-title")).ToContainTextAsync("Control Center");
    }

    [Test]
    public async Task AuthenticatedTechnician_CanAccessDashboard()
    {
        await LoginAsAsync("Technician");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
        await Expect(Page.Locator(".dash-title")).ToContainTextAsync("Control Center");
    }
}
