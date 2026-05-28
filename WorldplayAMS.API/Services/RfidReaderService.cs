using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class RfidReaderService : IRfidReaderService
{
    private readonly ISupabaseRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RfidReaderService> _logger;

    public RfidReaderService(ISupabaseRepository repository, IMemoryCache cache, ILogger<RfidReaderService> logger)
    {
        _repository = repository;
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
            var response = await _repository.GetTagByStringAsync(tagUid);

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
