using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

namespace WorldplayAMS.Core.Models;

[Table("game_sessions")]
public class GameSession : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("machine_id")]
    public Guid MachineId { get; set; }

    [Column("rfid_tag_id")]
    public Guid RfidTagId { get; set; }

    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Column("cost")]
    public decimal Cost { get; set; }
}
