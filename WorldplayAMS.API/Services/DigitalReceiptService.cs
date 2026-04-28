using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;
using WorldplayAMS.Core.Interfaces;

namespace WorldplayAMS.API.Services;

public class DigitalReceiptService
{
    private readonly ISupabaseRepository _repository;
    private readonly ILogger<DigitalReceiptService> _logger;

    public DigitalReceiptService(ISupabaseRepository repository, ILogger<DigitalReceiptService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Generates a digital receipt from a completed session.
    /// Called automatically during checkout — every checkout produces a receipt.
    /// Receipt number format: WP-YYYYMMDD-XXXX (date + 4-char hex from session ID).
    /// </summary>
    public async Task<DigitalReceipt?> GenerateReceiptAsync(Session session, string? machineName = null)
    {
        try
        {
            // Guard: only completed sessions with valid checkout data
            if (session.Status != "Completed" || !session.EndTime.HasValue || !session.Fee.HasValue)
            {
                _logger.LogWarning("Cannot generate receipt for session {SessionId} — not completed or missing data.", session.Id);
                return null;
            }

            // Check if receipt already exists for this session (idempotency)
            var existing = await _repository.GetReceiptBySessionIdAsync(session.Id);
            if (existing != null)
            {
                _logger.LogInformation("Receipt already exists for session {SessionId}: {ReceiptNumber}", session.Id, existing.ReceiptNumber);
                return existing;
            }

            var receiptNumber = GenerateReceiptNumber(session.Id, session.EndTime.Value);

            var receipt = new DigitalReceipt
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                ReceiptNumber = receiptNumber,
                RfidTagId = session.RfidTagId,
                GuestName = session.GuestName,
                MachineName = machineName,
                CheckInTime = session.StartTime,
                CheckOutTime = session.EndTime.Value,
                DurationMinutes = session.TotalDurationMinutes ?? 0,
                Fee = session.Fee.Value,
                StaffName = session.CheckedOutByStaff ?? "Unknown",
                IssuedAt = DateTime.UtcNow,
                Status = "Issued"
            };

            await _repository.InsertReceiptAsync(receipt);
            _logger.LogInformation(
                "Receipt {ReceiptNumber} generated for session {SessionId}. Guest: {Guest}, Machine: {Machine}, Fee: LKR {Fee:F2}",
                receiptNumber, session.Id, receipt.GuestName, receipt.MachineName ?? "N/A", receipt.Fee);

            return receipt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate receipt for session {SessionId}", session.Id);
            return null;
        }
    }

    /// <summary>
    /// Retrieves a receipt by session ID.
    /// </summary>
    public async Task<DigitalReceipt?> GetReceiptBySessionAsync(Guid sessionId)
    {
        try
        {
            return await _repository.GetReceiptBySessionIdAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch receipt for session {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// Retrieves receipts within the specified UTC date range.
    /// </summary>
    public async Task<List<DigitalReceipt>> GetReceiptsByDateRangeAsync(DateTime from, DateTime to)
    {
        try
        {
            return await _repository.GetReceiptsByDateRangeAsync(from, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch receipts for range {From} - {To}", from, to);
            return new List<DigitalReceipt>();
        }
    }

    /// <summary>
    /// Searches receipts by receipt number prefix or exact match.
    /// </summary>
    public async Task<List<DigitalReceipt>> SearchReceiptsAsync(string query)
    {
        try
        {
            // Try exact match first
            var exact = await _repository.GetReceiptByNumberAsync(query);
            if (exact != null)
                return new List<DigitalReceipt> { exact };

            // Fall back to date range of today (search within recent receipts)
            var recent = await _repository.GetReceiptsByDateRangeAsync(
                DateTime.UtcNow.Date.AddDays(-30),
                DateTime.UtcNow.Date.AddDays(1));

            var filtered = recent
                .Where(r => r.ReceiptNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            r.GuestName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (r.MachineName ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search receipts for query '{Query}'", query);
            return new List<DigitalReceipt>();
        }
    }

    /// <summary>
    /// Generates a unique receipt number in format: WP-YYYYMMDD-XXXX
    /// </summary>
    private static string GenerateReceiptNumber(Guid sessionId, DateTime checkoutTime)
    {
        var datePart = checkoutTime.ToString("yyyyMMdd");
        var hexSuffix = sessionId.ToString("N")[..4].ToUpper();
        return $"WP-{datePart}-{hexSuffix}";
    }
}
