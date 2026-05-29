using System;

namespace WorldplayAMS.Core.Models
{
    public class ExportJobRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Format { get; set; } = "CSV"; // "CSV" or "XLSX"
        public string Category { get; set; } = "Transactions"; // "Transactions", "Machines", "AuditLogs"
    }

    public class ExportJobStatus
    {
        public Guid JobId { get; set; }
        public string Status { get; set; } = "Pending"; // "Pending", "Processing", "Completed", "Failed"
        public string? ErrorMessage { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ContentType { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
    }
}
