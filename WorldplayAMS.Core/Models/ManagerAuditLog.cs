using Postgrest.Attributes;
using Postgrest.Models;

namespace WorldplayAMS.Core.Models;

[Table("managerauditlogs")]
public class ManagerAuditLog : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("managerid")]
    public Guid? ManagerId { get; set; }

    [Column("managername")]
    public string ManagerName { get; set; } = string.Empty;

    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("details")]
    public string? Details { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
