using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Access Denied page (/access-denied).
/// Verifies correct rendering of the denial message and navigation links.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AccessDeniedTests : WorldplayTestBase
{
    [Test]
    public async Task AccessDenied_ShouldDisplayCorrectTitle()
    {
        await LoginAsAsync("Staff");
        await NavigateToAsync("/access-denied");
        await WaitForBlazorAsync();

        await Expect(Page).ToHaveTitleAsync(new Regex("Access Denied"));
    }

    [Test]
    public async Task AccessDenied_ShouldShowDeniedHeading()
    {
        await LoginAsAsync("Staff");
        await NavigateToAsync("/access-denied");
        await WaitForBlazorAsync();

        var heading = Page.Locator(".denied-title");
        await Expect(heading).ToContainTextAsync("Access Denied");
    }

    [Test]
    public async Task AccessDenied_ShouldShowPermissionMessage()
    {
        await LoginAsAsync("Staff");
        await NavigateToAsync("/access-denied");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Your current role does not have permission")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AccessDenied_ShouldShowContactAdminMessage()
    {
        await LoginAsAsync("Staff");
        await NavigateToAsync("/access-denied");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Contact your system administrator")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AccessDenied_ShouldShowBackToDashboardLink()
    {
        await LoginAsAsync("Staff");
        await NavigateToAsync("/access-denied");
        await WaitForBlazorAsync();

        var backLink = Page.GetByRole(AriaRole.Link, new() { Name = "Back to Dashboard" });
        await Expect(backLink).ToBeVisibleAsync();
        await Expect(backLink).ToHaveAttributeAsync("href", "/");
    }

    [Test]
    public async Task AccessDenied_ShouldAppearWhenStaffAccessesAdminPage()
    {
        await LoginAsAsync("Staff");
        await NavigateToAsync("/admin/machines");
        await WaitForBlazorAsync();

        // The inline access denied component should render
        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AccessDenied_ShouldAppearWhenTechnicianAccessesAdminPage()
    {
        await LoginAsAsync("Technician");
        await NavigateToAsync("/admin/staff");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }
}
