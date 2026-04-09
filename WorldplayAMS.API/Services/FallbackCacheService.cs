using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace WorldplayAMS.API.Services;

public interface IFallbackCacheService
{
    void SaveFailedSession(string tagString, string actionType);
}

public class FallbackCacheService : IFallbackCacheService
{
    private readonly IMemoryCache _cache;

    public FallbackCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void SaveFailedSession(string tagString, string actionType)
    {
        // Locally store the payload if database drops
        var payload = new { TagString = tagString, ActionType = actionType, Timestamp = DateTime.UtcNow };

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromDays(1)); 

        _cache.Set($"offline_sync_{Guid.NewGuid()}", payload, cacheOptions);
    }
}
