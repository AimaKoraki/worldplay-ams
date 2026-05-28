using System;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;
using WorldplayAMS.Core.Interfaces;

namespace WorldplayAMS.API.Services
{
    public class SupabaseRepository : ISupabaseRepository
    {
        private readonly Supabase.Client _supabase;

        public SupabaseRepository(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<RfidTag?> GetActiveTagAsync(string tagString)
        {
            return await _supabase.From<RfidTag>()
                .Where(t => t.TagString == tagString && t.Status == "Active")
                .Single();
        }

        public async Task<RfidTag?> GetTagByStringAsync(string tagString)
        {
            return await _supabase.From<RfidTag>()
                .Where(t => t.TagString == tagString)
                .Single();
        }

        public async Task<Session?> GetActiveSessionAsync(Guid tagId)
        {
            return await _supabase.From<Session>()
                .Where(s => s.RfidTagId == tagId && s.Status == "Active")
                .Single();
        }

        public async Task InsertSessionAsync(Session session)
        {
            await _supabase.From<Session>().Insert(session);
        }

        public async Task UpdateSessionAsync(Session session)
        {
            await _supabase.From<Session>().Update(session);
        }

        public async Task<List<Session>> GetActiveSessionsAsync()
        {
            var response = await _supabase.From<Session>().Where(s => s.Status == "Active").Get();
            return response.Models ?? new List<Session>();
        }

        public async Task<List<Session>> GetCompletedSessionsAsync()
        {
            var response = await _supabase.From<Session>().Where(s => s.Status == "Completed").Get();
            return response.Models ?? new List<Session>();
        }

        public async Task<ArcadeMachine?> GetMachineAsync(Guid machineId)
        {
            return await _supabase.From<ArcadeMachine>()
                .Where(m => m.Id == machineId)
                .Single();
        }

        public async Task InsertMachineAsync(ArcadeMachine machine)
        {
            await _supabase.From<ArcadeMachine>().Insert(machine);
        }

        public async Task UpdateMachineAsync(ArcadeMachine machine)
        {
            await _supabase.From<ArcadeMachine>().Update(machine);
        }

        public async Task DeleteMachineAsync(Guid machineId)
        {
            await _supabase.From<ArcadeMachine>()
                .Where(m => m.Id == machineId)
                .Delete();
        }

        public async Task<List<ArcadeMachine>> GetAllMachinesAsync()
        {
            var response = await _supabase.From<ArcadeMachine>().Get();
            return response.Models ?? new List<ArcadeMachine>();
        }

        public async Task<MachineUsageLog?> GetActiveMachineLogAsync(Guid machineId)
        {
            return await _supabase.From<MachineUsageLog>()
                .Where(m => m.MachineId == machineId && m.Status == "Active")
                .Single();
        }

        public async Task InsertMachineLogAsync(MachineUsageLog log)
        {
            await _supabase.From<MachineUsageLog>().Insert(log);
        }

        public async Task UpdateMachineLogAsync(MachineUsageLog log)
        {
            await _supabase.From<MachineUsageLog>().Update(log);
        }

        public async Task<List<MachineUsageLog>> GetAllMachineUsageLogsAsync()
        {
            var response = await _supabase.From<MachineUsageLog>().Get();
            return response.Models ?? new List<MachineUsageLog>();
        }

        // DEV-13: Transaction History & Audit

        public async Task<List<Session>> GetSessionsByDateRangeAsync(DateTime from, DateTime to)
        {
            var response = await _supabase.From<Session>()
                .Where(s => s.Status == "Completed")
                .Filter("endtime", Postgrest.Constants.Operator.GreaterThanOrEqual, from.ToString("o"))
                .Filter("endtime", Postgrest.Constants.Operator.LessThanOrEqual, to.ToString("o"))
                .Order("endtime", Postgrest.Constants.Ordering.Descending)
                .Get();
            return response.Models ?? new List<Session>();
        }

        public async Task InsertAuditLogAsync(ManagerAuditLog log)
        {
            await _supabase.From<ManagerAuditLog>().Insert(log);
        }

        public async Task<List<ManagerAuditLog>> GetAuditLogsAsync(int limit = 50)
        {
            var response = await _supabase.From<ManagerAuditLog>()
                .Order("timestamp", Postgrest.Constants.Ordering.Descending)
                .Limit(limit)
                .Get();
            return response.Models ?? new List<ManagerAuditLog>();
        }

        // DEV-8: Digital Receipts

        public async Task InsertReceiptAsync(DigitalReceipt receipt)
        {
            await _supabase.From<DigitalReceipt>().Insert(receipt);
        }

        public async Task<DigitalReceipt?> GetReceiptBySessionIdAsync(Guid sessionId)
        {
            return await _supabase.From<DigitalReceipt>()
                .Where(r => r.SessionId == sessionId)
                .Single();
        }

        public async Task<List<DigitalReceipt>> GetReceiptsByDateRangeAsync(DateTime from, DateTime to)
        {
            var response = await _supabase.From<DigitalReceipt>()
                .Filter("issuedat", Postgrest.Constants.Operator.GreaterThanOrEqual, from.ToString("o"))
                .Filter("issuedat", Postgrest.Constants.Operator.LessThanOrEqual, to.ToString("o"))
                .Order("issuedat", Postgrest.Constants.Ordering.Descending)
                .Get();
            return response.Models ?? new List<DigitalReceipt>();
        }

        public async Task<DigitalReceipt?> GetReceiptByNumberAsync(string receiptNumber)
        {
            return await _supabase.From<DigitalReceipt>()
                .Where(r => r.ReceiptNumber == receiptNumber)
                .Single();
        }
    }
}
