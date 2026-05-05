using Postgrest.Attributes;
using Postgrest.Models;

namespace WorldplayAMS.Core.Models;

[Table("customers")]
public class Customer : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("firstname")]
    public string FirstName { get; set; } = string.Empty;

    [Column("lastname")]
    public string LastName { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [Column("phonenumber")]
    public string? PhoneNumber { get; set; }

    [Column("dob")]
    public DateTime? DOB { get; set; }

    [Column("type")]
    public string Type { get; set; } = "Regular";

    [Column("guardianid")]
    public Guid? GuardianId { get; set; }

    [Column("registrationdate")]
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
}
