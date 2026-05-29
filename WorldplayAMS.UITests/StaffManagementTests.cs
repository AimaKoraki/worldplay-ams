using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Staff Management page (/admin/staff).
/// Requires Admin role. Verifies page rendering, stats row,
/// staff table, add staff panel with form validation, and role-based access.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class StaffManagementTests : WorldplayTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginAsAsync("Admin");
        await NavigateToAsync("/admin/staff");
        await WaitForBlazorAsync();
    }

    [Test]
    public async Task StaffManagement_ShouldDisplayCorrectTitle()
    {
        await Expect(Page).ToHaveTitleAsync(new Regex("Staff Management"));
    }

    [Test]
    public async Task StaffManagement_ShouldShowAddStaffButton()
    {
        var addBtn = Page.Locator("#btn-add-staff");
        await Expect(addBtn).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_ShouldShowStatsRow()
    {
        var statsRow = Page.Locator(".staff-stats-row");
        await Expect(statsRow).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_ShouldShowStaffTable()
    {
        var table = Page.Locator("#staff-table");
        await Expect(table).ToBeVisibleAsync();

        // Verify table headers
        await Expect(Page.GetByRole(AriaRole.Columnheader, new() { Name = "Name" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Columnheader, new() { Name = "Role", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_AddButton_ShouldOpenCreatePanel()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        // Side panel should appear
        var panelOverlay = Page.Locator(".panel-overlay");
        await Expect(panelOverlay).ToBeVisibleAsync();

        // Should show panel title
        await Expect(Page.GetByText("New Staff Account")).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_CreatePanel_ShouldShowAllFormFields()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        // Verify all input fields are present by their IDs
        await Expect(Page.Locator("#input-staff-name")).ToBeVisibleAsync();
        await Expect(Page.Locator("#input-staff-email")).ToBeVisibleAsync();
        await Expect(Page.Locator("#input-staff-password")).ToBeVisibleAsync();
        await Expect(Page.Locator("#select-staff-role")).ToBeVisibleAsync();

        // Verify submit button
        await Expect(Page.Locator("#btn-create-staff-submit")).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_CreatePanel_ShouldShowPlaceholders()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        await Expect(Page.Locator("#input-staff-name")).ToHaveAttributeAsync("placeholder", "e.g. Kasun Perera");
        await Expect(Page.Locator("#input-staff-email")).ToHaveAttributeAsync("placeholder", "staff@worldplay.com");
        await Expect(Page.Locator("#input-staff-password")).ToHaveAttributeAsync("placeholder", "Min. 8 characters");
    }

    [Test]
    public async Task StaffManagement_CreatePanel_ShouldValidateName()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        // Fill email and password but leave name empty
        await Page.Locator("#input-staff-email").FillAsync("test@worldplay.com");
        await Page.Locator("#input-staff-password").FillAsync("password123");

        await Page.Locator("#btn-create-staff-submit").ClickAsync();

        // Should show validation error
        await Expect(Page.GetByText("Name is required")).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_CreatePanel_ShouldValidateEmail()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        // Fill name and password but leave email empty
        await Page.Locator("#input-staff-name").FillAsync("Test User");
        await Page.Locator("#input-staff-password").FillAsync("password123");

        await Page.Locator("#btn-create-staff-submit").ClickAsync();

        // Should show validation error
        await Expect(Page.GetByText("Email is required")).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_CreatePanel_ShouldValidatePasswordLength()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        // Fill name and email but use a short password
        await Page.Locator("#input-staff-name").FillAsync("Test User");
        await Page.Locator("#input-staff-email").FillAsync("test@worldplay.com");
        await Page.Locator("#input-staff-password").FillAsync("short");

        await Page.Locator("#btn-create-staff-submit").ClickAsync();

        // Should show validation error
        await Expect(Page.GetByText("Password must be at least 8 characters")).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_CreatePanel_ShouldHaveRoleOptions()
    {
        await Page.Locator("#btn-add-staff").ClickAsync();

        var roleSelect = Page.Locator("#select-staff-role");

        // Verify the role dropdown has the expected options
        var options = roleSelect.Locator("option");
        await Expect(options).ToHaveCountAsync(3);
    }

    [Test]
    public async Task StaffManagement_ShouldBeDeniedForStaffRole()
    {
        await NavigateToAsync("/login");
        await LoginAsAsync("Staff");
        await NavigateToAsync("/admin/staff");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }

    [Test]
    public async Task StaffManagement_ShouldBeDeniedForTechnicianRole()
    {
        await NavigateToAsync("/login");
        await LoginAsAsync("Technician");
        await NavigateToAsync("/admin/staff");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }
}
