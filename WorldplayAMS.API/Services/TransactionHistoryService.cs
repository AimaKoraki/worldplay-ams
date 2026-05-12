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
            
            var totalSessions = sessions.Count;
            var totalRevenue = sessions.Sum(s => s.Fee ?? 0);
            var totalDuration = sessions.Sum(s => s.TotalDurationMinutes ?? 0);
            var averageDuration = totalSessions > 0 ? (double)totalDuration / totalSessions : 0;
            
            var highestFee = sessions.Any() ? sessions.Max(s => s.Fee ?? 0) : 0;
            var longestSession = sessions.Any() ? sessions.Max(s => s.TotalDurationMinutes ?? 0) : 0;

            int peakHour = 0;
            string peakHourDisplay = "N/A";
            
            if (sessions.Any(s => s.EndTime.HasValue))
            {
                var peakGroup = sessions
                    .Where(s => s.EndTime.HasValue)
                    .GroupBy(s => s.EndTime!.Value.ToLocalTime().Hour)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                    
                if (peakGroup != null)
                {
                    peakHour = peakGroup.Key;
                    var amPm = peakHour >= 12 ? "PM" : "AM";
                    var hour12 = peakHour % 12;
                    if (hour12 == 0) hour12 = 12;
                    peakHourDisplay = $"{hour12}:00 {amPm}";
                }
            }

            return new 
            {
                Date = date.Date,
                TotalSessions = totalSessions,
                TotalRevenue = totalRevenue,
                AverageDurationMinutes = averageDuration,
                PeakCheckOutHour = peakHour,
                PeakCheckOutHourDisplay = peakHourDisplay,
                HighestSingleFee = highestFee,
                LongestSessionMinutes = longestSession
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get daily summary.");
            return new { Error = "Failed to generate summary" };
        }
    }

    public async Task LogManagerActionAsync(string managerName, string action, string? details, Guid? staffId = null)
    {
        try
        {
            var log = new ManagerAuditLog
            {
                Id = Guid.NewGuid(),
                ManagerId = staffId == Guid.Empty ? null : staffId,
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
