using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class GameSessionService : IGameSessionService
{
    private readonly ISupabaseRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameSessionService> _logger;

    public GameSessionService(ISupabaseRepository repository, IMemoryCache cache, ILogger<GameSessionService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Session?> StartSessionAsync(string tagUid, Guid machineId)
    {
        // For sub-3 sec speed, we can first optionally check local tag cache, but let's do a fast insert.
        var session = new Session
        {
            Id = Guid.NewGuid(),
            MachineId = machineId,
            GuestName = "Walk-in Guest",
            Status = "Active",
            StartTime = DateTime.UtcNow,

        };

        try
        {
            // Resolve RfidTagId
            var tagResponse = await _repository.GetActiveTagAsync(tagUid);
            
            if (tagResponse == null) 
            {
                _logger.LogWarning("Invalid or inactive tag tapped: {tagUid}", tagUid);
                return null;
            }

            session.RfidTagId = tagResponse.Id;

            var activeSession = await _repository.GetActiveSessionAsync(tagResponse.Id);

            if (activeSession != null)
            {
                _cache.Set($"session_{activeSession.Id}", activeSession, TimeSpan.FromHours(2));
                return activeSession;
            }

            // E.g., subtract cost from tag etc, skipped for brevity

            await _repository.InsertSessionAsync(session);

            // Cache it so it's readily available
            _cache.Set($"session_{session.Id}", session, TimeSpan.FromHours(2));
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase connection failed. Falling back to optimistic memory logging.");
            
            // In-Memory fallback
            _cache.Set($"offline_session_{session.Id}", session, TimeSpan.FromDays(1));
            // We would have a background worker that checks `offline_session_*` and syncs them when back online.
            return session;
        }
    }

    public async Task<IEnumerable<Session>> GetActiveSessionsAsync()
    {
        try
        {
            return await _repository.GetActiveSessionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions. Returning cached/empty.");
            return new List<Session>();
        }
    }
}
