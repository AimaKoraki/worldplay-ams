using Postgrest.Attributes;
using Postgrest.Models;

namespace WorldplayAMS.Core.Models;

[Table("digitalreceipts")]
public class DigitalReceipt : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("sessionid")]
    public Guid SessionId { get; set; }

    [Column("receiptnumber")]
    public string ReceiptNumber { get; set; } = string.Empty;

    [Column("rfidtagid")]
    public Guid RfidTagId { get; set; }

    [Column("guestname")]
    public string GuestName { get; set; } = "Walk-in Guest";

    [Column("machinename")]
    public string? MachineName { get; set; }

    [Column("checkintime")]
    public DateTime CheckInTime { get; set; }

    [Column("checkouttime")]
    public DateTime CheckOutTime { get; set; }

    [Column("durationminutes")]
    public int DurationMinutes { get; set; }

    [Column("fee")]
    public decimal Fee { get; set; }

    [Column("staffname")]
    public string StaffName { get; set; } = string.Empty;

    [Column("issuedat")]
    public DateTime IssuedAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Issued";
}
