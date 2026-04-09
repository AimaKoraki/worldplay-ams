using System;
using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Core.Interfaces;

public interface IGameSessionService
{
    /// <summary>
    /// Starts a game session for a specific machine using an RFID tag.
    /// Resilient: Falls back to memory cache if Supabase is down.
    /// </summary>
    Task<GameSession?> StartSessionAsync(string tagUid, Guid machineId);

    /// <summary>
    /// Retrieves active sessions for monitoring.
    /// </summary>
    Task<IEnumerable<GameSession>> GetActiveSessionsAsync();
}
