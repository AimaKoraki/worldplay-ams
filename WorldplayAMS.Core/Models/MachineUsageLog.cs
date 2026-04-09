using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace WorldplayAMS.Core.Models;

[Table("machineusagelogs")]
public class MachineUsageLog : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("machineid")]
    public Guid MachineId { get; set; }

    [Column("starttime")]
    public DateTime StartTime { get; set; }

    [Column("endtime")]
    public DateTime? EndTime { get; set; }

    [Column("durationminutes")]
    public int? DurationMinutes { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Active";
}
