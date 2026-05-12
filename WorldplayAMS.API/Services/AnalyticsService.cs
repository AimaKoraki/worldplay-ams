using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class AnalyticsService
{
    private readonly ISupabaseRepository _repository;
    private readonly MachineMonitoringService _machineService;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(ISupabaseRepository repository, MachineMonitoringService machineService, ILogger<AnalyticsService> logger)
    {
        _repository = repository;
        _machineService = machineService;
        _logger = logger;
    }

    public async Task<object> GetPeakHoursAsync(DateTime from, DateTime to)
    {
        try
        {
            var sessions = await _repository.GetSessionsByDateRangeAsync(from, to);

            // Group by the hour of the day using StartTime
            var startHourGroups = sessions
                .GroupBy(s => s.StartTime.ToLocalTime().Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    SessionCount = g.Count(),
                    DisplayHour = FormatHour(g.Key)
                })
                .OrderBy(x => x.Hour)
                .ToList();

            // Find the absolute peak hour
            var peakHour = startHourGroups.OrderByDescending(x => x.SessionCount).FirstOrDefault();

            return new
            {
                DateRange = new { From = from, To = to },
                PeakHour = peakHour,
                HourlyData = startHourGroups
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to aggregate peak hours.");
            return new { Error = "Failed to aggregate peak hours data." };
        }
    }

    public async Task<object> GetMachineUsageAnalyticsAsync(DateTime from, DateTime to)
    {
        try
        {
            var sessions = await _repository.GetSessionsByDateRangeAsync(from, to);
            var machines = await _machineService.GetAllMachinesAsync();
            var machineMap = machines.ToDictionary(m => m.Id, m => m.Name);

            var machineUsage = sessions
                .Where(s => s.MachineId.HasValue)
                .GroupBy(s => s.MachineId!.Value)
                .Select(g => new
                {
                    MachineId = g.Key,
                    MachineName = machineMap.TryGetValue(g.Key, out var name) ? name : "Unknown Machine",
                    TotalSessions = g.Count(),
                    TotalDurationMinutes = g.Sum(s => s.TotalDurationMinutes ?? 0),
                    TotalRevenue = g.Sum(s => s.Fee ?? 0),
                    AverageDurationMinutes = g.Any() ? g.Average(s => s.TotalDurationMinutes ?? 0) : 0
                })
                .OrderByDescending(x => x.TotalSessions)
                .ToList();

            return new
            {
                DateRange = new { From = from, To = to },
                MachineUsage = machineUsage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to aggregate machine usage analytics.");
            return new { Error = "Failed to aggregate machine usage data." };
        }
    }

    private string FormatHour(int hour)
    {
        var amPm = hour >= 12 ? "PM" : "AM";
        var hour12 = hour % 12;
        if (hour12 == 0) hour12 = 12;
        return $"{hour12}:00 {amPm}";
    }
}
