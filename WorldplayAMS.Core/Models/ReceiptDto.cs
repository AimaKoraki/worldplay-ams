using System;

namespace WorldplayAMS.Core.Models;

public class ReceiptDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid RfidTagId { get; set; }
    public string GuestName { get; set; } = "Walk-in Guest";
    public string? MachineName { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime CheckOutTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Fee { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public string Status { get; set; } = "Issued";
}
