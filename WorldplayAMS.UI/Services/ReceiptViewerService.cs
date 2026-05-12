using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.UI.Services;

/// <summary>
/// Concrete implementation of IReceiptViewerService. Handles API communication.
/// </summary>
public class ReceiptViewerService : IReceiptViewerService
{
    private readonly IHttpClientFactory _clientFactory;

    public ReceiptViewerService(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<List<ReceiptDto>> GetReceiptsAsync(DateTime from, DateTime to)
    {
        var client = _clientFactory.CreateClient("ApiClient");
        try
        {
            var fromStr = from.ToString("yyyy-MM-dd");
            var toStr = to.AddDays(1).AddTicks(-1).ToString("o");
            var result = await client.GetFromJsonAsync<List<ReceiptDto>>($"/api/receipts?from={fromStr}&to={toStr}");
            return result ?? new List<ReceiptDto>();
        }
        catch
        {
            return new List<ReceiptDto>();
        }
    }

    public async Task<ReceiptDto?> GetReceiptBySessionIdAsync(Guid sessionId)
    {
        var client = _clientFactory.CreateClient("ApiClient");
        try
        {
            var result = await client.GetFromJsonAsync<ReceiptDto>($"/api/receipts/{sessionId}");
            return result;
        }
        catch
        {
            return null;
        }
    }
}
