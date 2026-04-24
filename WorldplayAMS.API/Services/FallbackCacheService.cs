using System.Collections.Concurrent;

namespace WorldplayAMS.API.Services;

/// <summary>
/// Stores failed session payloads locally when Supabase is unreachable.
/// Uses a thread-safe in-memory queue so the BackgroundSyncService can retrieve and retry them.
/// </summary>
public interface IFallbackCacheService
{
    void SaveFailedSession(string tagString, string actionType);
    List<OfflinePayload> GetPendingPayloads();
    void RemovePayload(string id);
}

public class OfflinePayload
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TagString { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class FallbackCacheService : IFallbackCacheService
{
    // Thread-safe dictionary keyed by payload ID for O(1) lookup/removal
    private static readonly ConcurrentDictionary<string, OfflinePayload> _pendingPayloads = new();

    public void SaveFailedSession(string tagString, string actionType)
    {
        var payload = new OfflinePayload
        {
            TagString = tagString,
            ActionType = actionType,
            Timestamp = DateTime.UtcNow
        };

        _pendingPayloads.TryAdd(payload.Id, payload);
    }

    public List<OfflinePayload> GetPendingPayloads()
    {
        return _pendingPayloads.Values.OrderBy(p => p.Timestamp).ToList();
    }

    public void RemovePayload(string id)
    {
        _pendingPayloads.TryRemove(id, out _);
    }
}
