using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class SessionManagerService
{
    private readonly Supabase.Client _supabase;
    private readonly IFallbackCacheService _fallbackCache;
    private readonly ILogger<SessionManagerService> _logger;
    private readonly decimal _ratePerMinute;

    public SessionManagerService(Supabase.Client supabase, IFallbackCacheService fallbackCache, ILogger<SessionManagerService> logger, IConfiguration configuration)
    {
        _supabase = supabase;
        _fallbackCache = fallbackCache;
        _logger = logger;
        _ratePerMinute = configuration.GetValue<decimal>("Billing:RatePerMinute", 0.15m);
    }

    public async Task<string> ProcessRfidTapAsync(string tagString)
    {
        try
        {
            // 1. Validate Tag
            var tagResponse = await _supabase.From<RfidTag>()
                .Where(t => t.TagString == tagString && t.Status == "Active")
                .Single();

            if (tagResponse == null) return "Error: Invalid or inactive RFID tag.";

            // 2. Check for active session
            var activeSessionResponse = await _supabase.From<Session>()
                .Where(s => s.RfidTagId == tagResponse.Id && s.Status == "Active")
                .Single();

            if (activeSessionResponse == null)
            {
                // Check-in
                var newSession = new Session
                {
                    Id = Guid.NewGuid(),
                    RfidTagId = tagResponse.Id,
                    StartTime = DateTime.UtcNow,
                    Status = "Active"
                };

                await _supabase.From<Session>().Insert(newSession);
                return "Success: Checked in!";
            }
            else
            {
                // Check-out
                var session = activeSessionResponse;
                session.EndTime = DateTime.UtcNow;
                session.Status = "Completed";
                session.TotalDurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;

                // Calculate fee based on duration and configured rate
                session.Fee = session.TotalDurationMinutes * _ratePerMinute;

                // Update is performed directly on the mapped model with Postgrest
                await _supabase.From<Session>().Update(session);
                return $"Success: Checked out. Duration: {session.TotalDurationMinutes} min | Fee: ${session.Fee:F2}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase connection failed. Queuing payload.");
            _fallbackCache.SaveFailedSession(tagString, "CheckInOutTap");
            return "Offline: Tap recorded locally. Will sync when online.";
        }
    }

    public async Task<List<Session>> GetActiveSessionsAsync()
    {
        try
        {
            var response = await _supabase.From<Session>()
                .Where(s => s.Status == "Active")
                .Get();
            return response.Models ?? new List<Session>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active sessions");
            return new List<Session>();
        }
    }

    public async Task<List<Session>> GetCompletedSessionsAsync()
    {
        try
        {
            var response = await _supabase.From<Session>()
                .Where(s => s.Status == "Completed")
                .Get();
            return response.Models ?? new List<Session>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session history");
            return new List<Session>();
        }
    }

    public async Task<decimal> GetTodayRevenueAsync()
    {
        try
        {
            var sessions = await GetCompletedSessionsAsync();
            return sessions
                .Where(s => s.EndTime.HasValue && s.EndTime.Value.Date == DateTime.UtcNow.Date)
                .Sum(s => s.Fee ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate today's revenue");
            return 0;
        }
    }
}
