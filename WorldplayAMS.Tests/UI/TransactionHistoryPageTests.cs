using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using WorldplayAMS.UI.Components.Pages;

namespace WorldplayAMS.Tests.UI;

public class TransactionHistoryPageTests : BunitContext
{
    private void SetupServices()
    {
        // Register required services
        Services.AddHttpClient("ApiClient", client =>
        {
            client.BaseAddress = new Uri("http://localhost:5089");
        });

        // Set up Admin authentication state for authorized rendering
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Test Admin"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var authState = Task.FromResult(new AuthenticationState(user));
        var mockAuthProvider = new Moq.Mock<AuthenticationStateProvider>();
        mockAuthProvider.Setup(a => a.GetAuthenticationStateAsync()).Returns(authState);
        Services.AddSingleton<AuthenticationStateProvider>(mockAuthProvider.Object);
    }

    [Fact]
    public void TransactionPage_RendersPageTitle()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var title = cut.Find(".txn-title");
        title.TextContent.Should().Contain("Transaction History");
    }

    [Fact]
    public void TransactionPage_RendersDateFilters()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var fromInput = cut.Find("#input-date-from");
        fromInput.Should().NotBeNull();

        var toInput = cut.Find("#input-date-to");
        toInput.Should().NotBeNull();
    }

    [Fact]
    public void TransactionPage_RendersQuickFilterButtons()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var buttons = cut.FindAll(".btn-quick");
        buttons.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void TransactionPage_RendersSummaryCards()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var cards = cut.FindAll(".recon-card");
        cards.Should().HaveCount(4);
    }

    [Fact]
    public void TransactionPage_RendersExportButton()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var exportBtn = cut.Find("#btn-export-csv");
        exportBtn.Should().NotBeNull();
        exportBtn.TextContent.Should().Contain("Export CSV");
    }

    [Fact]
    public void TransactionPage_RendersAuditTrailButton()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var auditBtn = cut.Find("#btn-toggle-audit");
        auditBtn.Should().NotBeNull();
        auditBtn.TextContent.Should().Contain("Audit Trail");
    }

    [Fact]
    public void TransactionPage_RendersSearchInput()
    {
        SetupServices();
        var cut = Render<TransactionHistory>();

        var searchInput = cut.Find("#input-search");
        searchInput.Should().NotBeNull();
    }
}
