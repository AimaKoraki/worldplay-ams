using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.UI.Services;

/// <summary>
/// Service for fetching digital receipt data, adhering to the Dependency Inversion principle.
/// </summary>
public interface IReceiptViewerService
{
    /// <summary>
    /// Fetches all receipts within a given date range.
    /// </summary>
    Task<List<ReceiptDto>> GetReceiptsAsync(DateTime from, DateTime to);

    /// <summary>
    /// Fetches a specific receipt by its associated Session ID.
    /// </summary>
    Task<ReceiptDto?> GetReceiptBySessionIdAsync(Guid sessionId);
}
