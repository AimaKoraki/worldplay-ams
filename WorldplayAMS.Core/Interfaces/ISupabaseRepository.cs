using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Core.Interfaces
{
    public interface ISupabaseRepository
    {
        Task<RfidTag?> GetActiveTagAsync(string tagString);
        Task<RfidTag?> GetTagByStringAsync(string tagString);
        Task<Session?> GetActiveSessionAsync(Guid tagId);
        Task InsertSessionAsync(Session session);
        Task UpdateSessionAsync(Session session);
        Task<List<Session>> GetActiveSessionsAsync();
        Task<List<Session>> GetCompletedSessionsAsync();
        
        Task<ArcadeMachine?> GetMachineAsync(Guid machineId);
        Task InsertMachineAsync(ArcadeMachine machine);
        Task UpdateMachineAsync(ArcadeMachine machine);
        Task DeleteMachineAsync(Guid machineId);
        Task<List<ArcadeMachine>> GetAllMachinesAsync();
        
        Task<MachineUsageLog?> GetActiveMachineLogAsync(Guid machineId);
        Task InsertMachineLogAsync(MachineUsageLog log);
        Task UpdateMachineLogAsync(MachineUsageLog log);
        Task<List<MachineUsageLog>> GetAllMachineUsageLogsAsync();

        // DEV-13: Transaction History & Audit
        Task<List<Session>> GetSessionsByDateRangeAsync(DateTime from, DateTime to);
        Task InsertAuditLogAsync(ManagerAuditLog log);
        Task<List<ManagerAuditLog>> GetAuditLogsAsync(int limit = 50);

        // DEV-8: Digital Receipts
        Task InsertReceiptAsync(DigitalReceipt receipt);
        Task<DigitalReceipt?> GetReceiptBySessionIdAsync(Guid sessionId);
        Task<List<DigitalReceipt>> GetReceiptsByDateRangeAsync(DateTime from, DateTime to);
        Task<DigitalReceipt?> GetReceiptByNumberAsync(string receiptNumber);
    }
}
