using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Login page (/login).
/// Verifies form rendering, validation, DEV mode quick-access buttons,
/// and authentication flow.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class LoginTests : WorldplayTestBase
{
    [Test]
    public async Task LoginPage_ShouldDisplayCorrectTitle()
    {
        await NavigateToAsync("/login");
        await WaitForBlazorAsync();

        await Expect(Page).ToHaveTitleAsync(new Regex("Worldplay AMS"));
    }

    [Test]
    public async Task LoginPage_ShouldShowStaffPortalHeading()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Staff Portal" });
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginPage_ShouldShowLoginDescription()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        var desc = Page.GetByText("Sign in with your credentials to continue");
        await Expect(desc).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginPage_ShouldShowEmailAndPasswordInputs()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        var emailInput = Page.Locator("#input-email");
        var passwordInput = Page.Locator("#input-password");

        await Expect(emailInput).ToBeVisibleAsync();
        await Expect(passwordInput).ToBeVisibleAsync();

        // Check placeholders
        await Expect(emailInput).ToHaveAttributeAsync("placeholder", "you@worldplay.com");
        await Expect(passwordInput).ToHaveAttributeAsync("placeholder", "Enter your password");
    }

    [Test]
    public async Task LoginPage_ShouldShowSignInButton()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        var signInBtn = Page.Locator("#btn-login");
        await Expect(signInBtn).ToBeVisibleAsync();
        await Expect(signInBtn).ToContainTextAsync("Sign In");
    }

    [Test]
    public async Task LoginPage_ShouldShowValidationError_WhenFieldsEmpty()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        // Click Sign In without filling in any fields
        await Page.Locator("#btn-login").ClickAsync();

        // Should display validation error
        var error = Page.Locator(".login-error");
        await Expect(error).ToBeVisibleAsync();
        await Expect(error).ToContainTextAsync("Please enter both email and password");
    }

    [Test]
    public async Task LoginPage_ShouldShowDevModeQuickAccessButtons()
    {
        // This test only passes in Development environment
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        // Check for DEV MODE section
        var devDivider = Page.GetByText("DEV MODE — Quick Access");
        await Expect(devDivider).ToBeVisibleAsync();

        // Check for role buttons
        var adminBtn = Page.Locator(".role-admin");
        var techBtn = Page.Locator(".role-tech");
        var staffBtn = Page.Locator(".role-staff");

        await Expect(adminBtn).ToBeVisibleAsync();
        await Expect(techBtn).ToBeVisibleAsync();
        await Expect(staffBtn).ToBeVisibleAsync();

        // Verify role labels
        await Expect(adminBtn).ToContainTextAsync("Administrator");
        await Expect(techBtn).ToContainTextAsync("Technician");
        await Expect(staffBtn).ToContainTextAsync("Staff");
    }

    [Test]
    public async Task LoginPage_ShouldShowSecurityFooter()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        var footer = Page.Locator(".login-footer");
        await Expect(footer).ToContainTextAsync("Secured via Supabase Auth");
    }

    [Test]
    public async Task LoginPage_AdminQuickAccess_ShouldRedirectToDashboard()
    {
        await LoginAsAsync("Admin");

        // Should be on the dashboard after login
        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
    }

    [Test]
    public async Task LoginPage_StaffQuickAccess_ShouldRedirectToDashboard()
    {
        await LoginAsAsync("Staff");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
    }

    [Test]
    public async Task LoginPage_TechnicianQuickAccess_ShouldRedirectToDashboard()
    {
        await LoginAsAsync("Technician");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/$"));
    }

    [Test]
    public async Task LoginPage_ShouldSubmitOnEnterKey()
    {
        await NavigateToAsync("/login");
        await Page.WaitForSelectorAsync(".login-card", new() { Timeout = 10000 });

        // Type into email and press Enter without filling password
        await Page.Locator("#input-email").FillAsync("test@example.com");
        await Page.Locator("#input-email").PressAsync("Enter");

        // Should show validation error since password is empty
        var error = Page.Locator(".login-error");
        await Expect(error).ToBeVisibleAsync();
        await Expect(error).ToContainTextAsync("Please enter both email and password");
    }
}
