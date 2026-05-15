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

    public async Task<object> GetStaffingRecommendationsAsync(DateTime from, DateTime to)
    {
        try
        {
            var sessions = await _repository.GetSessionsByDateRangeAsync(from, to);

            // Calculate how many weeks are in the date range to get a true average
            var totalDays = (to - from).TotalDays;
            var weeksInRange = Math.Max(1, totalDays / 7.0);

            // Group by Day of Week and Hour
            var hourlyData = sessions
                .GroupBy(s => new { s.StartTime.ToLocalTime().DayOfWeek, s.StartTime.ToLocalTime().Hour })
                .Select(g => 
                {
                    var totalSessions = g.Count();
                    var avgSessions = (int)Math.Round(totalSessions / weeksInRange);
                    // Ensure it doesn't drop to 0 if there's at least some traffic
                    if (totalSessions > 0 && avgSessions == 0) avgSessions = 1;

                    return new
                    {
                        DayOfWeek = g.Key.DayOfWeek.ToString(),
                        Hour = g.Key.Hour,
                        DisplayHour = FormatHour(g.Key.Hour),
                        AverageSessions = avgSessions,
                        RecommendedStaff = CalculateRecommendedStaff(avgSessions)
                    };
                })
                .OrderBy(x => DayOfWeekToNumber(x.DayOfWeek))
                .ThenBy(x => x.Hour)
                .ToList();

            return new
            {
                DateRange = new { From = from, To = to },
                Recommendations = hourlyData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate staffing recommendations.");
            return new { Error = "Failed to calculate staffing recommendations." };
        }
    }

    private int DayOfWeekToNumber(string day)
    {
        return day switch
        {
            "Monday" => 1,
            "Tuesday" => 2,
            "Wednesday" => 3,
            "Thursday" => 4,
            "Friday" => 5,
            "Saturday" => 6,
            "Sunday" => 7,
            _ => 8
        };
    }

    private int CalculateRecommendedStaff(int averageSessions)
    {
        // Base staffing is 2. We add 1 staff member for every 5 active sessions.
        int baseStaff = 2;
        int sessionsPerStaff = 5;
        
        int additionalStaff = averageSessions / sessionsPerStaff;
        return baseStaff + additionalStaff; 
    }

    private string FormatHour(int hour)
    {
        var amPm = hour >= 12 ? "PM" : "AM";
        var hour12 = hour % 12;
        if (hour12 == 0) hour12 = 12;
        return $"{hour12}:00 {amPm}";
    }
}
