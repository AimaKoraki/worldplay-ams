using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

namespace WorldplayAMS.Core.Models;

[Table("arcademachines")]
public class ArcadeMachine : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("machinetype")]
    public string MachineType { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "Online";
}
