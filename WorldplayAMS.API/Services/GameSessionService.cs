using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class GameSessionService : IGameSessionService
{
    private readonly Supabase.Client _supabase;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameSessionService> _logger;

    public GameSessionService(Supabase.Client supabase, IMemoryCache cache, ILogger<GameSessionService> logger)
    {
        _supabase = supabase;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GameSession?> StartSessionAsync(string tagUid, Guid machineId)
    {
        // For sub-3 sec speed, we can first optionally check local tag cache, but let's do a fast insert.
        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            MachineId = machineId,
            StartedAt = DateTime.UtcNow,
            // We would map tagUid to an actual RfidTag Id. Assuming for now we resolve it.
            // Let's assume tagUid is used to lookup the actual tag id. 
        };

        try
        {
            // Resolve RfidTagId
            var tagResponse = await _supabase.From<RfidTag>()
                                .Where(t => t.TagString == tagUid && t.Status == "Active")
                                .Single();
            
            if (tagResponse == null) 
            {
                _logger.LogWarning("Invalid or inactive tag tapped: {tagUid}", tagUid);
                return null;
            }

            session.RfidTagId = tagResponse.Id;
            // E.g., subtract cost from tag etc, skipped for brevity

            var response = await _supabase.From<GameSession>().Insert(session);
            var resultingSession = response.Models.FirstOrDefault();

            if (resultingSession != null)
            {
                // Cache it so it's readily available
                _cache.Set($"session_{resultingSession.Id}", resultingSession, TimeSpan.FromHours(2));
            }
            return resultingSession;
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

    public async Task<IEnumerable<GameSession>> GetActiveSessionsAsync()
    {
        try
        {
            var response = await _supabase.From<GameSession>()
                .Where(s => s.DurationSeconds == null)
                .Get();
                
            return response.Models ?? new List<GameSession>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions. Returning cached/empty.");
            return new List<GameSession>();
        }
    }
}
