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

    public async Task<PeakHoursDto?> GetPeakHoursAsync(DateTime from, DateTime to)
    {
        try
        {
            var sessions = await _repository.GetSessionsByDateRangeAsync(from, to);

            // Group by the hour of the day using StartTime
            var startHourGroups = sessions
                .GroupBy(s => s.StartTime.ToLocalTime().Hour)
                .Select(g => new HourlyDataDto
                {
                    Hour = g.Key,
                    SessionCount = g.Count(),
                    DisplayHour = FormatHour(g.Key)
                })
                .OrderBy(x => x.Hour)
                .ToList();

            // Find the absolute peak hour
            var peakHour = startHourGroups.OrderByDescending(x => x.SessionCount).FirstOrDefault();

            // DEV-09: Build Matrix for 7 columns (Monday-Sunday) and 14 hours (09:00 - 22:00 -> meaning 9 to 22 which is 14 rows, or 09:00 to 23:00 -> wait 09:00 to 22:00 inclusive is 14 rows)
            var matrix = new List<PeakHourMatrixCellDto>();
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
            var activeMachinesCount = (await _machineService.GetAllMachinesAsync()).Count;
            if (activeMachinesCount == 0) activeMachinesCount = 1; // avoid divide by zero

            // Calculate max session count per hour across all data to normalize density if needed, or use activeMachinesCount
            // We'll base occupancy on SessionCount / activeMachinesCount.
            
            foreach (var day in days)
            {
                for (int hour = 9; hour <= 22; hour++)
                {
                    var count = sessions.Count(s => s.StartTime.ToLocalTime().DayOfWeek == day && s.StartTime.ToLocalTime().Hour == hour);
                    double occupancy = (double)count / activeMachinesCount * 100;
                    
                    matrix.Add(new PeakHourMatrixCellDto
                    {
                        DayOfWeek = day.ToString(),
                        Hour = hour,
                        DisplayHour = FormatHour(hour),
                        SessionCount = count,
                        OccupancyPercentage = occupancy,
                        EstimatedGuestCount = (int)Math.Round(count * 1.2) // Estimate 1.2 guests per session
                    });
                }
            }

            var maxSessionCount = matrix.Any() ? matrix.Max(m => m.SessionCount) : 0;

            return new PeakHoursDto
            {
                DateRange = new DateRangeDto { From = from, To = to },
                PeakHour = peakHour,
                HourlyData = startHourGroups,
                Matrix = matrix,
                MaxSessionCount = maxSessionCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to aggregate peak hours.");
            return null;
        }
    }

    public async Task<MachineUsageAnalyticsDto?> GetMachineUsageAnalyticsAsync(DateTime from, DateTime to)
    {
        try
        {
            var sessions = await _repository.GetSessionsByDateRangeAsync(from, to);
            var machines = await _machineService.GetAllMachinesAsync();
            var machineMap = machines.ToDictionary(m => m.Id, m => m.Name);

            var machineUsage = sessions
                .Where(s => s.MachineId.HasValue)
                .GroupBy(s => s.MachineId!.Value)
                .Select(g => new MachineUsageDto
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

            return new MachineUsageAnalyticsDto
            {
                DateRange = new DateRangeDto { From = from, To = to },
                MachineUsage = machineUsage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to aggregate machine usage analytics.");
            return null;
        }
    }

    public async Task<StaffingRecommendationsDto?> GetStaffingRecommendationsAsync(DateTime from, DateTime to)
    {
        try
        {
            // Enforce rolling 28-day window
            to = DateTime.UtcNow;
            from = to.AddDays(-28);

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

                    int confScore = totalSessions > 0 ? Math.Min(98, 75 + totalSessions) : 60;

                    return new StaffingRecommendationDataDto
                    {
                        DayOfWeek = g.Key.DayOfWeek.ToString(),
                        Hour = g.Key.Hour,
                        DisplayHour = FormatHour(g.Key.Hour),
                        AverageSessions = avgSessions,
                        RecommendedStaff = CalculateRecommendedStaff(avgSessions),
                        Confidence = $"Confidence: {confScore}%"
                    };
                })
                .OrderBy(x => DayOfWeekToNumber(x.DayOfWeek))
                .ThenBy(x => x.Hour)
                .ToList();

            return new StaffingRecommendationsDto
            {
                DateRange = new DateRangeDto { From = from, To = to },
                Recommendations = hourlyData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate staffing recommendations.");
            return null;
        }
    }

    public async Task<RevPAMHAnalyticsDto?> GetRevPAMHAsync(DateTime from, DateTime to, string? category = null)
    {
        try
        {
            var sessions = await _repository.GetSessionsByDateRangeAsync(from, to);
            var machines = await _machineService.GetAllMachinesAsync();
            var activeMachines = machines.Where(m => m.Status == "Online").ToList();
            
            var totalHours = (to - from).TotalHours;
            if (totalHours <= 0) totalHours = 24; // Default to 24 hours if same day or invalid

            var totalRevenue = sessions.Sum(s => s.Fee ?? 0);
            var activeMachineCount = activeMachines.Count;
            
            var totalAvailableMachineHours = activeMachineCount * totalHours;
            var systemRevPAMH = totalAvailableMachineHours > 0 
                ? totalRevenue / (decimal)totalAvailableMachineHours 
                : 0;

            // Calculate per machine RevPAMH
            var machineMap = machines.ToDictionary(m => m.Id, m => new { m.Name, m.Category });
            var machineUsageQuery = sessions.Where(s => s.MachineId.HasValue);
            
            var machineUsage = machineUsageQuery
                .GroupBy(s => s.MachineId!.Value)
                .Select(g => 
                {
                    var machineInfo = machineMap.TryGetValue(g.Key, out var info) ? info : null;
                    return new MachineRevPAMHDto
                    {
                        MachineId = g.Key,
                        MachineName = machineInfo?.Name ?? "Unknown Machine",
                        Category = machineInfo?.Category ?? "Uncategorized",
                        TotalRevenue = g.Sum(s => s.Fee ?? 0),
                        RevPAMH = g.Sum(s => s.Fee ?? 0) / (decimal)totalHours
                    };
                });

            if (!string.IsNullOrEmpty(category) && category != "All")
            {
                machineUsage = machineUsage.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            var filteredMachineUsage = machineUsage.OrderByDescending(x => x.RevPAMH).ToList();

            return new RevPAMHAnalyticsDto
            {
                DateRange = new DateRangeDto { From = from, To = to },
                TotalHours = totalHours,
                ActiveMachineCount = activeMachineCount,
                TotalAvailableMachineHours = totalAvailableMachineHours,
                TotalRevenue = totalRevenue,
                SystemRevPAMH = systemRevPAMH,
                MachineRevPAMH = filteredMachineUsage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate RevPAMH.");
            return null;
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
        if (averageSessions == 0) return 0;
        return Math.Max(1, (int)Math.Ceiling(averageSessions / 15.0));
    }

    private string FormatHour(int hour)
    {
        var amPm = hour >= 12 ? "PM" : "AM";
        var hour12 = hour % 12;
        if (hour12 == 0) hour12 = 12;
        return $"{hour12}:00 {amPm}";
    }
}
