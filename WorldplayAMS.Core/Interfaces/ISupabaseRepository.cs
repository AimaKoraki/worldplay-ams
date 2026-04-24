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
        Task UpdateMachineAsync(ArcadeMachine machine);
        Task<List<ArcadeMachine>> GetAllMachinesAsync();
        
        Task<MachineUsageLog?> GetActiveMachineLogAsync(Guid machineId);
        Task InsertMachineLogAsync(MachineUsageLog log);
        Task UpdateMachineLogAsync(MachineUsageLog log);
        Task<List<MachineUsageLog>> GetAllMachineUsageLogsAsync();

        // DEV-13: Transaction History & Audit
        Task<List<Session>> GetSessionsByDateRangeAsync(DateTime from, DateTime to);
        Task InsertAuditLogAsync(ManagerAuditLog log);
        Task<List<ManagerAuditLog>> GetAuditLogsAsync(int limit = 50);
    }
}
