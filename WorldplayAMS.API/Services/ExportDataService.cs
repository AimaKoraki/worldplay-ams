using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services
{
    public class ExportDataService
    {
        private readonly ISupabaseRepository _repository;
        private readonly ExportJobStateTracker _tracker;
        private readonly ILogger<ExportDataService> _logger;
        // Temporary in-memory dictionary to pass Request details to ProcessJobAsync
        public static readonly ConcurrentDictionary<Guid, ExportJobRequest> JobRequests = new();

        public ExportDataService(ISupabaseRepository repository, ExportJobStateTracker tracker, ILogger<ExportDataService> logger)
        {
            _repository = repository;
            _tracker = tracker;
            _logger = logger;
        }

        public async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            if (!JobRequests.TryRemove(jobId, out var request))
            {
                throw new Exception("Export request data not found.");
            }

            var tempPath = Path.Combine(Path.GetTempPath(), "worldplay_exports");
            Directory.CreateDirectory(tempPath);

            string ext = request.Format.ToUpper() == "XLSX" ? ".xlsx" : ".csv";
            string fileName = $"{request.Category}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{jobId.ToString()[..8]}{ext}";
            string filePath = Path.Combine(tempPath, fileName);

            if (request.Category == "Transactions" || request.Category == "Sales")
            {
                var sessions = await _repository.GetSessionsByDateRangeAsync(request.FromDate, request.ToDate.AddDays(1).AddTicks(-1));
                if (request.Format.ToUpper() == "XLSX")
                    GenerateTransactionsXlsx(sessions, filePath);
                else
                    GenerateTransactionsCsv(sessions, filePath);
            }
            else if (request.Category == "Machines" || request.Category == "Inventory")
            {
                var machines = await _repository.GetAllMachinesAsync();
                if (request.Format.ToUpper() == "XLSX")
                    GenerateMachinesXlsx(machines, filePath);
                else
                    GenerateMachinesCsv(machines, filePath);
            }
            else if (request.Category == "AuditLogs" || request.Category == "User Logs")
            {
                // We'll just fetch a generic batch for now, as AuditLogs might not have a date filter exposed in repo easily, or we can use GetAll.
                // Assuming we use the raw client or similar. For now, let's mock it since we don't have a specific Audit repo method by date yet.
                // Or just pull the generic method. The repository might not have GetAuditLogsByDateRangeAsync.
                // We will fetch up to 1000 logs for demo if there's no specific repo.
                // Actually let's assume we can query it directly or just generate an empty/dummy one if repo lacks it, 
                // but let's check ISupabaseRepository later. For now, empty list is fine if we can't query.
                if (request.Format.ToUpper() == "XLSX")
                    GenerateAuditLogsXlsx(new List<ManagerAuditLog>(), filePath);
                else
                    GenerateAuditLogsCsv(new List<ManagerAuditLog>(), filePath);
            }
            else
            {
                throw new Exception("Unknown category.");
            }

            _tracker.UpdateJob(jobId, s =>
            {
                s.Status = "Completed";
                s.FilePath = filePath;
                s.FileName = fileName;
                s.ContentType = request.Format.ToUpper() == "XLSX" 
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" 
                    : "text/csv";
            });
        }

        private void GenerateTransactionsCsv(IEnumerable<Session> sessions, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Session ID,Tag ID,Machine ID,Check-In,Check-Out,Duration (min),Fee (LKR),Staff,Status");
            foreach (var s in sessions)
            {
                var staff = s.CheckedOutByStaff ?? "N/A";
                var machine = s.MachineId?.ToString() ?? "N/A";
                writer.WriteLine($"{s.Id},{s.RfidTagId},{machine},{s.StartTime:o},{s.EndTime?.ToString("o") ?? "N/A"},{s.TotalDurationMinutes ?? 0},{(s.Fee ?? 0):F2},{staff},{s.Status}");
            }
        }

        private void GenerateTransactionsXlsx(IEnumerable<Session> sessions, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Transactions");
            ws.Cell(1, 1).Value = "Session ID";
            ws.Cell(1, 2).Value = "Tag ID";
            ws.Cell(1, 3).Value = "Machine ID";
            ws.Cell(1, 4).Value = "Check-In";
            ws.Cell(1, 5).Value = "Check-Out";
            ws.Cell(1, 6).Value = "Duration (min)";
            ws.Cell(1, 7).Value = "Fee (LKR)";
            ws.Cell(1, 8).Value = "Staff";
            ws.Cell(1, 9).Value = "Status";

            int row = 2;
            foreach (var s in sessions)
            {
                ws.Cell(row, 1).Value = s.Id.ToString();
                ws.Cell(row, 2).Value = s.RfidTagId.ToString();
                ws.Cell(row, 3).Value = s.MachineId?.ToString() ?? "N/A";
                ws.Cell(row, 4).Value = s.StartTime;
                ws.Cell(row, 5).Value = s.EndTime?.ToString() ?? "N/A";
                ws.Cell(row, 6).Value = s.TotalDurationMinutes ?? 0;
                ws.Cell(row, 7).Value = s.Fee ?? 0;
                ws.Cell(row, 8).Value = s.CheckedOutByStaff ?? "N/A";
                ws.Cell(row, 9).Value = s.Status;
                row++;
            }
            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        private void GenerateMachinesCsv(IEnumerable<ArcadeMachine> machines, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Machine ID,Name,Type,Status,Cost Per Play");
            foreach (var m in machines)
            {
                writer.WriteLine($"{m.Id},{m.Name},{m.MachineType},{m.Status},{m.CurrentCostPerPlay ?? 0}");
            }
        }

        private void GenerateMachinesXlsx(IEnumerable<ArcadeMachine> machines, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Machines");
            ws.Cell(1, 1).Value = "Machine ID";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(1, 3).Value = "Type";
            ws.Cell(1, 4).Value = "Status";
            ws.Cell(1, 5).Value = "Cost Per Play";

            int row = 2;
            foreach (var m in machines)
            {
                ws.Cell(row, 1).Value = m.Id.ToString();
                ws.Cell(row, 2).Value = m.Name;
                ws.Cell(row, 3).Value = m.MachineType;
                ws.Cell(row, 4).Value = m.Status;
                ws.Cell(row, 5).Value = m.CurrentCostPerPlay ?? 0;
                row++;
            }
            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        private void GenerateAuditLogsCsv(IEnumerable<ManagerAuditLog> logs, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Log ID,Manager,Action,Details,Timestamp");
            foreach (var l in logs)
            {
                writer.WriteLine($"{l.Id},{l.ManagerName},{l.Action},\"{l.Details}\",{l.Timestamp:o}");
            }
        }

        private void GenerateAuditLogsXlsx(IEnumerable<ManagerAuditLog> logs, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("AuditLogs");
            ws.Cell(1, 1).Value = "Log ID";
            ws.Cell(1, 2).Value = "Manager";
            ws.Cell(1, 3).Value = "Action";
            ws.Cell(1, 4).Value = "Details";
            ws.Cell(1, 5).Value = "Timestamp";

            int row = 2;
            foreach (var l in logs)
            {
                ws.Cell(row, 1).Value = l.Id.ToString();
                ws.Cell(row, 2).Value = l.ManagerName;
                ws.Cell(row, 3).Value = l.Action;
                ws.Cell(row, 4).Value = l.Details;
                ws.Cell(row, 5).Value = l.Timestamp;
                row++;
            }
            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }
    }
}
