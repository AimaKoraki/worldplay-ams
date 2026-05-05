using Postgrest.Attributes;
using Postgrest.Models;

namespace WorldplayAMS.Core.Models;

[Table("zones")]
public class Zone : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("zonename")]
    public string ZoneName { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }
}
