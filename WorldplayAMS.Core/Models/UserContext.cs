using Postgrest.Attributes;
using Postgrest.Models;

namespace WorldplayAMS.Core.Models;

[Table("user_contexts")]
public class UserContext : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("systemrole")]
    public string SystemRole { get; set; } = "Staff";
}
