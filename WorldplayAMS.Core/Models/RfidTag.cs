using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

namespace WorldplayAMS.Core.Models;

[Table("rfidtags")]
public class RfidTag : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("tagstring")]
    public string TagString { get; set; } = string.Empty;

    [Column("userid")]
    public Guid? UserId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Active";
}

[Table("sessions")]
public class Session : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("rfidtagid")]
    public Guid RfidTagId { get; set; }

    [Column("starttime")]
    public DateTime StartTime { get; set; }

    [Column("endtime")]
    public DateTime? EndTime { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Active";

    [Column("totaldurationminutes")]
    public int? TotalDurationMinutes { get; set; }
}
