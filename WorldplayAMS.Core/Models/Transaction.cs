using Postgrest.Attributes;
using Postgrest.Models;

namespace WorldplayAMS.Core.Models;

[Table("transactions")]
public class Transaction : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("sessionid")]
    public Guid? SessionId { get; set; }

    [Column("customerid")]
    public Guid? CustomerId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("paymentmethod")]
    public string PaymentMethod { get; set; } = "Cash";

    [Column("status")]
    public string Status { get; set; } = "Completed";

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
