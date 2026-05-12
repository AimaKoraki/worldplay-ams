using System;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Core.Interfaces;

public interface IGameSessionService
{
    /// <summary>
    /// Starts a session for a specific machine using an RFID tag.
    /// Resilient: Falls back to memory cache if Supabase is down.
    /// </summary>
    Task<Session?> StartSessionAsync(string tagUid, Guid machineId);

    /// <summary>
    /// Retrieves active sessions for monitoring.
    /// </summary>
    Task<IEnumerable<Session>> GetActiveSessionsAsync();
}
