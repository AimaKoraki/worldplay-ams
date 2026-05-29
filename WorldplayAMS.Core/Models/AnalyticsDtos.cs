using System;
using System.Collections.Generic;

namespace WorldplayAMS.Core.Models
{
    public class DateRangeDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public class HourlyDataDto
    {
        public int Hour { get; set; }
        public int SessionCount { get; set; }
        public string DisplayHour { get; set; } = string.Empty;
    }

    public class PeakHourMatrixCellDto
    {
        public string DayOfWeek { get; set; } = string.Empty;
        public int Hour { get; set; }
        public string DisplayHour { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public double OccupancyPercentage { get; set; }
        public int EstimatedGuestCount { get; set; }
    }

    public class PeakHoursDto
    {
        public DateRangeDto DateRange { get; set; } = new();
        public HourlyDataDto? PeakHour { get; set; }
        public List<HourlyDataDto> HourlyData { get; set; } = new();
        public List<PeakHourMatrixCellDto> Matrix { get; set; } = new();
        public int MaxSessionCount { get; set; }
    }

    public class MachineUsageDto
    {
        public Guid MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public int TotalDurationMinutes { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageDurationMinutes { get; set; }
    }

    public class MachineUsageAnalyticsDto
    {
        public DateRangeDto DateRange { get; set; } = new();
        public List<MachineUsageDto> MachineUsage { get; set; } = new();
    }

    public class StaffingRecommendationDataDto
    {
        public string DayOfWeek { get; set; } = string.Empty;
        public int Hour { get; set; }
        public string DisplayHour { get; set; } = string.Empty;
        public int AverageSessions { get; set; }
        public int RecommendedStaff { get; set; }
        public string Confidence { get; set; } = string.Empty;
    }

    public class StaffingRecommendationsDto
    {
        public DateRangeDto DateRange { get; set; } = new();
        public List<StaffingRecommendationDataDto> Recommendations { get; set; } = new();
    }

    public class MachineRevPAMHDto
    {
        public Guid MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal RevPAMH { get; set; }
    }

    public class RevPAMHAnalyticsDto
    {
        public DateRangeDto DateRange { get; set; } = new();
        public double TotalHours { get; set; }
        public int ActiveMachineCount { get; set; }
        public double TotalAvailableMachineHours { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal SystemRevPAMH { get; set; }
        public List<MachineRevPAMHDto> MachineRevPAMH { get; set; } = new();
    }
    
    public class DailyReconciliationSummaryDto
    {
        public DateTime Date { get; set; }
        public int TotalSessions { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageDurationMinutes { get; set; }
        public int PeakCheckOutHour { get; set; }
        public string PeakCheckOutHourDisplay { get; set; } = string.Empty;
        public decimal HighestSingleFee { get; set; }
        public int LongestSessionMinutes { get; set; }
    }
}
