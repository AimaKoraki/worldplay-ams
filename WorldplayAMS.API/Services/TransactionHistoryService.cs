using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

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

    public async Task<List<Session>> GetTransactionsByDateRangeAsync(DateTime from, DateTime to)
    {
        try
        {
            return await _repository.GetSessionsByDateRangeAsync(from, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transactions by date range.");
            return new List<Session>();
        }
    }

    public async Task<object> GetDailyReconciliationSummaryAsync(DateTime date)
    {
        try
        {
            var start = date.Date;
            var end = start.AddDays(1).AddTicks(-1);
            var sessions = await _repository.GetSessionsByDateRangeAsync(start, end);
            
            var totalRevenue = sessions.Sum(s => s.Fee ?? 0);
            var completedSessions = sessions.Count(s => s.Status == "Completed");
            var activeSessions = sessions.Count(s => s.Status == "Active");
            var totalDuration = sessions.Sum(s => s.TotalDurationMinutes ?? 0);

            return new 
            {
                Date = date.Date,
                TotalRevenue = totalRevenue,
                CompletedSessions = completedSessions,
                ActiveSessions = activeSessions,
                TotalDurationMinutes = totalDuration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get daily summary.");
            return new { Error = "Failed to generate summary" };
        }
    }

    public async Task LogManagerActionAsync(string managerName, string action, string? details)
    {
        try
        {
            var log = new ManagerAuditLog
            {
                Id = Guid.NewGuid(),
                ManagerId = Guid.Empty,
                ManagerName = managerName,
                Action = action,
                Details = details ?? "",
                Timestamp = DateTime.UtcNow
            };
            await _repository.InsertAuditLogAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log manager action.");
        }
    }

    public async Task<List<ManagerAuditLog>> GetManagerAuditLogsAsync(int limit)
    {
        try
        {
            return await _repository.GetAuditLogsAsync(limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs.");
            return new List<ManagerAuditLog>();
        }
    }
}
