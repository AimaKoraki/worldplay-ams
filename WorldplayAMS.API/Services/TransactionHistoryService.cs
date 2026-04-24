using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;
using WorldplayAMS.Core.Interfaces;

namespace WorldplayAMS.API.Services;

public class TransactionHistoryService
{
    private readonly ISupabaseRepository _repository;
    private readonly ILogger<TransactionHistoryService> _logger;

    public TransactionHistoryService(ISupabaseRepository repository, ILogger<TransactionHistoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Returns completed sessions within the specified UTC date range.
    /// </summary>
    public async Task<List<Session>> GetTransactionsByDateRangeAsync(DateTime from, DateTime to)
    {
        try
        {
            return await _repository.GetSessionsByDateRangeAsync(from, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch transactions for range {From} - {To}", from, to);
            return new List<Session>();
        }
    }

    /// <summary>
    /// Computes a daily reconciliation summary for the given UTC date.
    /// </summary>
    public async Task<TransactionSummary> GetDailyReconciliationSummaryAsync(DateTime date)
    {
        try
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1).AddTicks(-1);

            var sessions = await _repository.GetSessionsByDateRangeAsync(dayStart, dayEnd);

            var summary = new TransactionSummary
            {
                Date = date.Date,
                TotalSessions = sessions.Count,
                TotalRevenue = sessions.Sum(s => s.Fee ?? 0),
                AverageDurationMinutes = sessions.Count > 0
                    ? sessions.Average(s => s.TotalDurationMinutes ?? 0)
                    : 0,
                PeakCheckOutHour = sessions.Count > 0
                    ? sessions
                        .Where(s => s.EndTime.HasValue)
                        .GroupBy(s => s.EndTime!.Value.Hour)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault()
                    : 0,
                HighestSingleFee = sessions.Count > 0
                    ? sessions.Max(s => s.Fee ?? 0)
                    : 0,
                LongestSessionMinutes = sessions.Count > 0
                    ? sessions.Max(s => s.TotalDurationMinutes ?? 0)
                    : 0
            };

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute daily summary for {Date}", date);
            return new TransactionSummary { Date = date.Date };
        }
    }

    /// <summary>
    /// Logs a manager-level action to the audit trail.
    /// Every action is timestamped per AGENTS.md compliance.
    /// </summary>
    public async Task LogManagerActionAsync(string managerName, string action, string? details = null)
    {
        try
        {
            var log = new ManagerAuditLog
            {
                Id = Guid.NewGuid(),
                ManagerName = managerName,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            await _repository.InsertAuditLogAsync(log);
            _logger.LogInformation("Audit log: Manager '{Manager}' performed '{Action}' at {Time:u}",
                managerName, action, log.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert audit log for manager '{Manager}'", managerName);
        }
    }

    /// <summary>
    /// Retrieves recent manager audit log entries.
    /// </summary>
    public async Task<List<ManagerAuditLog>> GetManagerAuditLogsAsync(int limit = 50)
    {
        try
        {
            return await _repository.GetAuditLogsAsync(limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch audit logs");
            return new List<ManagerAuditLog>();
        }
    }
}
