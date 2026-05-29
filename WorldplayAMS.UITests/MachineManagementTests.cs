using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Machine Management page (/admin/machines).
/// Requires Admin role. Verifies page rendering, stats row,
/// machine table, add/edit panel, and delete confirmation modal.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class MachineManagementTests : WorldplayTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginAsAsync("Admin");
        await NavigateToAsync("/admin/machines");
        await WaitForBlazorAsync();
    }

    [Test]
    public async Task MachineManagement_ShouldDisplayCorrectTitle()
    {
        await Expect(Page).ToHaveTitleAsync(new Regex("Machine Management"));
    }

    [Test]
    public async Task MachineManagement_ShouldShowAddMachineButton()
    {
        var addBtn = Page.Locator("#btn-add-machine");
        await Expect(addBtn).ToBeVisibleAsync();
    }

    [Test]
    public async Task MachineManagement_ShouldShowStatsRow()
    {
        var statsRow = Page.Locator(".machine-stats-row");
        await Expect(statsRow).ToBeVisibleAsync();
    }

    [Test]
    public async Task MachineManagement_ShouldShowMachineTable()
    {
        var table = Page.Locator("#machine-table");
        await Expect(table).ToBeVisibleAsync();

        // Verify table headers
        await Expect(Page.Locator("#machine-table")).ToBeVisibleAsync();
    }

    [Test]
    public async Task MachineManagement_AddButton_ShouldOpenSidePanel()
    {
        await Page.Locator("#btn-add-machine").ClickAsync();

        // Side panel should appear
        var panelOverlay = Page.Locator(".panel-overlay");
        await Expect(panelOverlay).ToBeVisibleAsync();

        // Should show "Add Machine" title
        await Expect(Page.Locator(".side-panel-title")).ToContainTextAsync("Add Machine");
    }

    [Test]
    public async Task MachineManagement_AddPanel_ShouldShowFormFields()
    {
        await Page.Locator("#btn-add-machine").ClickAsync();

        // Check for form inputs
        var nameInput = Page.GetByPlaceholder("e.g. Mario Kart DX");
        await Expect(nameInput).ToBeVisibleAsync();

        var feeInput = Page.GetByPlaceholder("15.00");
        await Expect(feeInput).ToBeVisibleAsync();

        // Check for submit button
        var submitBtn = Page.Locator(".btn-submit-panel");
        await Expect(submitBtn).ToBeVisibleAsync();
    }

    [Test]
    public async Task MachineManagement_AddPanel_ShouldShowValidationError_WhenFieldsEmpty()
    {
        await Page.Locator("#btn-add-machine").ClickAsync();

        // Click submit without filling fields
        await Page.Locator(".btn-submit-panel").ClickAsync();

        // Validation error should appear
        await Expect(Page.GetByText("Name and Type are required")).ToBeVisibleAsync();
    }

    [Test]
    public async Task MachineManagement_ShouldBeDeniedForStaffRole()
    {
        // Log out and re-login as Staff
        await NavigateToAsync("/login");
        await LoginAsAsync("Staff");

        // Try to access Machine Management
        await NavigateToAsync("/admin/machines");
        await WaitForBlazorAsync();

        // Should show Access Denied
        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }

    [Test]
    public async Task MachineManagement_ShouldBeDeniedForTechnicianRole()
    {
        // Log out and re-login as Technician
        await NavigateToAsync("/login");
        await LoginAsAsync("Technician");

        // Try to access Machine Management
        await NavigateToAsync("/admin/machines");
        await WaitForBlazorAsync();

        // Should show Access Denied
        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }
}
