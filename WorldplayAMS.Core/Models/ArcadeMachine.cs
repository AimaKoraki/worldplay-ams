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

    [Column("category")]
    public string? Category { get; set; }

    [Column("installationdate")]
    public DateTime? InstallationDate { get; set; }

    [Column("lastservicedate")]
    public DateTime? LastServiceDate { get; set; }

    [Column("currentcostperplay")]
    public decimal? CurrentCostPerPlay { get; set; }

    [Column("zoneid")]
    public Guid? ZoneId { get; set; }
}
