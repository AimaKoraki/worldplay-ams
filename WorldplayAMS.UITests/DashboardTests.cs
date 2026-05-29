using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WorldplayAMS.UITests;

/// <summary>
/// Tests for the Dashboard / Home page (/).
/// Requires authentication. Verifies KPI cards, layout, navigation links,
/// and session table rendering.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class DashboardTests : WorldplayTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        // Log in as Admin before each test to access the full dashboard
        await LoginAsAsync("Admin");
    }

    [Test]
    public async Task Dashboard_ShouldDisplayCorrectTitle()
    {
        await Expect(Page).ToHaveTitleAsync(new Regex("Worldplay AMS"));
    }

    [Test]
    public async Task Dashboard_ShouldShowControlCenterHeading()
    {
        var title = Page.Locator(".dash-title");
        await Expect(title).ToContainTextAsync("Control Center");
    }

    [Test]
    public async Task Dashboard_ShouldShowDashboardSubtitle()
    {
        var subtitle = Page.Locator(".dash-subtitle");
        await Expect(subtitle).ToContainTextAsync("Real-time overview");
    }

    [Test]
    public async Task Dashboard_ShouldDisplayKpiCards()
    {
        var kpiGrid = Page.Locator(".kpi-grid");
        await Expect(kpiGrid).ToBeVisibleAsync();

        // Verify the four KPI labels are present using HasText
        await Expect(Page.Locator(".kpi-label").Filter(new() { HasText = "Active Sessions" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".kpi-label").Filter(new() { HasText = "Players Online" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".kpi-label").Filter(new() { HasText = "Total Machines" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".kpi-label").Filter(new() { HasText = "Today's Revenue" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Dashboard_ShouldShowRefreshButton()
    {
        var refreshBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });
        await Expect(refreshBtn).ToBeVisibleAsync();
    }

    [Test]
    public async Task Dashboard_ShouldShowQuickActionLinks()
    {
        // Use Locator and HasText for quick action links since GetByRole matching might be strict
        var scannerLink = Page.Locator(".action-tile").Filter(new() { HasText = "Open Scanner" });
        await Expect(scannerLink).ToBeVisibleAsync();

        var diagLink = Page.Locator(".action-tile").Filter(new() { HasText = "Diagnostics Panel" });
        await Expect(diagLink).ToBeVisibleAsync();

        var historyLink = Page.Locator(".action-tile").Filter(new() { HasText = "Session History" });
        await Expect(historyLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task Dashboard_ShouldShowSessionsEmptyState()
    {
        var emptyState = Page.GetByRole(AriaRole.Heading, new() { Name = "No Active Sessions" });
        await Expect(emptyState).ToBeVisibleAsync();
    }

    [Test]
    public async Task Dashboard_ShouldShowRevenueInsightsSection()
    {
        var revenueSection = Page.GetByText("Revenue Insights");
        await Expect(revenueSection).ToBeVisibleAsync();
    }
}
