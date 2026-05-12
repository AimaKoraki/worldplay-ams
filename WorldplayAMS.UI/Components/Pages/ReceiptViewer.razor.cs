using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WorldplayAMS.Core.Models;
using WorldplayAMS.UI.Services;

namespace WorldplayAMS.UI.Components.Pages;

/// <summary>
/// Code-behind for ReceiptViewer. Handles UI state and logic, delegating data access to IReceiptViewerService.
/// This separates the presentation layer from business/data logic.
/// </summary>
public partial class ReceiptViewer
{
    [Inject] private IReceiptViewerService ReceiptService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // UI State
    protected List<ReceiptDto> allReceipts = new();
    protected List<ReceiptDto> filteredReceipts = new();
    protected ReceiptDto? selectedReceipt;
    protected DateTime dateFrom = DateTime.UtcNow.Date;
    protected DateTime dateTo = DateTime.UtcNow.Date;
    protected string searchQuery = string.Empty;
    protected string activeQuick = "today";
    protected bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        // Check for ?session= query param (deep link from TransactionHistory)
        var uri = new Uri(Nav.Uri);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sessionParam = queryParams["session"];

        await RefreshAll();

        if (!string.IsNullOrEmpty(sessionParam) && Guid.TryParse(sessionParam, out var sessionId))
        {
            await LoadSingleReceipt(sessionId);
        }
    }

    private async Task LoadSingleReceipt(Guid sessionId)
    {
        var result = await ReceiptService.GetReceiptBySessionIdAsync(sessionId);
        if (result != null)
        {
            selectedReceipt = result;
        }
    }

    protected async Task RefreshAll()
    {
        isLoading = true;
        await LoadReceipts();
        ApplySearchFilter();
        isLoading = false;
    }

    protected async Task ApplyFilter()
    {
        activeQuick = "";
        isLoading = true;
        await LoadReceipts();
        ApplySearchFilter();
        isLoading = false;
    }

    private async Task LoadReceipts()
    {
        allReceipts = await ReceiptService.GetReceiptsAsync(dateFrom, dateTo);
    }

    private void ApplySearchFilter()
    {
        var query = searchQuery?.Trim().ToLowerInvariant() ?? "";
        filteredReceipts = string.IsNullOrEmpty(query)
            ? allReceipts
            : allReceipts.Where(r =>
                r.ReceiptNumber.ToLowerInvariant().Contains(query) ||
                r.GuestName.ToLowerInvariant().Contains(query) ||
                (r.MachineName ?? "").ToLowerInvariant().Contains(query) ||
                r.StaffName.ToLowerInvariant().Contains(query)
            ).ToList();
    }

    protected void SelectReceipt(ReceiptDto receipt) { selectedReceipt = receipt; }
    protected void CloseDetail() { selectedReceipt = null; }

    private async Task SetQuickFilter(string mode)
    {
        activeQuick = mode;
        dateTo = DateTime.UtcNow.Date;
        dateFrom = mode switch
        {
            "today" => DateTime.UtcNow.Date,
            "week" => DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek),
            "month" => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
            _ => DateTime.UtcNow.Date
        };
        
        isLoading = true;
        await LoadReceipts();
        ApplySearchFilter();
        isLoading = false;
    }

    protected async Task QuickToday() => await SetQuickFilter("today");
    protected async Task QuickWeek() => await SetQuickFilter("week");
    protected async Task QuickMonth() => await SetQuickFilter("month");
    
    protected string QuickBtnClass(string mode) => activeQuick == mode ? "btn-quick-active" : "";

    protected async Task CopyPublicLink()
    {
        if (selectedReceipt == null) return;
        var url = $"{Nav.BaseUri}r/{selectedReceipt.SessionId}";
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", url);
    }

    /// <summary>
    /// Triggers the browser's native Print dialog scoped to the receipt-paper element.
    /// </summary>
    protected async Task PrintReceipt()
    {
        if (selectedReceipt == null) return;
        await JSRuntime.InvokeVoidAsync("window.print");
    }
}
