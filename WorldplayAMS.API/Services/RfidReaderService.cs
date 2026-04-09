using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class RfidReaderService : IRfidReaderService
{
    private readonly Supabase.Client _supabase;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RfidReaderService> _logger;

    public RfidReaderService(Supabase.Client supabase, IMemoryCache cache, ILogger<RfidReaderService> logger)
    {
        _supabase = supabase;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RfidTag?> ValidateTagAsync(string tagUid)
    {
        string cacheKey = $"rfid_{tagUid}";

        if (_cache.TryGetValue(cacheKey, out RfidTag? cachedTag))
        {
            return cachedTag;
        }

        try
        {
            var response = await _supabase.From<RfidTag>()
                .Where(t => t.TagString == tagUid)
                .Single();

            if (response != null)
            {
                // Cache the tag for fast subsequent reads (under 3 seconds performance requirement)
                _cache.Set(cacheKey, response, TimeSpan.FromMinutes(10));
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase connection failed while fetching RFID tag.");
            return null;
        }
    }
}
