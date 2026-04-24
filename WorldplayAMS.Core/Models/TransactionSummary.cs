namespace WorldplayAMS.Core.Models;

/// <summary>
/// DTO for daily reconciliation summary, computed from completed sessions.
/// Not a Postgrest model — this is calculated server-side.
/// </summary>
public class TransactionSummary
{
    public DateTime Date { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public double AverageDurationMinutes { get; set; }
    public int PeakCheckOutHour { get; set; }
    public string PeakCheckOutHourDisplay => PeakCheckOutHour == 0 ? "N/A" : $"{PeakCheckOutHour:00}:00";
    public decimal HighestSingleFee { get; set; }
    public int LongestSessionMinutes { get; set; }
}
