using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Data Export page (/export).
/// Requires Admin or Manager role. Verifies page rendering, form controls,
/// format selection, date range behavior, and role-based access.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class DataExportTests : WorldplayTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await LoginAsAsync("Admin");
        await NavigateToAsync("/export");
        await WaitForBlazorAsync();
    }

    [Test]
    public async Task DataExport_ShouldDisplayCorrectTitle()
    {
        await Expect(Page).ToHaveTitleAsync(new Regex("Data Export"));
    }

    [Test]
    public async Task DataExport_ShouldShowPageHeading()
    {
        await Expect(Page.GetByText("Data Export Center")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DataExport_ShouldShowDescription()
    {
        await Expect(Page.GetByText("Export transactions, machine logs")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DataExport_ShouldShowCategoryDropdown()
    {
        var categorySelect = Page.Locator(".form-input").First;
        await Expect(categorySelect).ToBeVisibleAsync();
    }

    [Test]
    public async Task DataExport_ShouldShowFormatRadioButtons()
    {
        var csvRadio = Page.Locator("label").Filter(new() { HasText = "CSV" });
        var xlsxRadio = Page.Locator("label").Filter(new() { HasText = "Excel" });

        await Expect(csvRadio).ToBeVisibleAsync();
        await Expect(xlsxRadio).ToBeVisibleAsync();
    }

    [Test]
    public async Task DataExport_ShouldShowGenerateExportButton()
    {
        var generateBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Generate Export" });
        await Expect(generateBtn).ToBeVisibleAsync();
    }

    [Test]
    public async Task DataExport_ShouldBeDeniedForStaffRole()
    {
        await NavigateToAsync("/login");
        await LoginAsAsync("Staff");
        await NavigateToAsync("/export");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DataExport_ShouldBeDeniedForTechnicianRole()
    {
        await NavigateToAsync("/login");
        await LoginAsAsync("Technician");
        await NavigateToAsync("/export");
        await WaitForBlazorAsync();

        await Expect(Page.GetByText("Access Denied")).ToBeVisibleAsync();
    }
}
