using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

<<<<<<< Updated upstream
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
                return $"Success: Checked out. Duration: {session.TotalDurationMinutes} min | Fee: LKR {session.Fee:F2}";
            }
        }
=======
    public class SessionManagerService
    {
        private readonly ISupabaseRepository _repository;
        private readonly IFallbackCacheService _fallbackCache;
        private readonly DigitalReceiptService _receiptService;
        private readonly ILogger<SessionManagerService> _logger;
        private readonly decimal _ratePerMinute;

        public SessionManagerService(
            ISupabaseRepository repository,
            IFallbackCacheService fallbackCache,
            DigitalReceiptService receiptService,
            ILogger<SessionManagerService> logger,
            IConfiguration configuration)
        {
            _repository = repository;
            _fallbackCache = fallbackCache;
            _receiptService = receiptService;
            _logger = logger;
            _ratePerMinute = configuration.GetValue<decimal>("Billing:RatePerMinute", 0.15m);
        }

        public async Task<string> ProcessRfidTapAsync(string tagString, string? staffName = null, string? guestName = null, Guid? machineId = null)
        {
            try
            {
                // First check if the tag exists at all (regardless of status)
                var anyTag = await _repository.GetTagByStringAsync(tagString);
                if (anyTag == null) return "Error: RFID tag not found in system.";
                if (anyTag.Status == "Lost") return "Error: This RFID tag has been reported lost. Please contact a manager.";
                if (anyTag.Status != "Active") return $"Error: RFID tag is currently '{anyTag.Status}'. Cannot process.";

                var tagResponse = anyTag;

                var activeSessionResponse = await _repository.GetActiveSessionAsync(tagResponse.Id);

                if (activeSessionResponse == null)
                {
                    // Check-in: create new session with optional guest name and machine assignment
                    var newSession = new Session
                    {
                        Id = Guid.NewGuid(),
                        RfidTagId = tagResponse.Id,
                        StartTime = DateTime.UtcNow,
                        Status = "Active",
                        GuestName = guestName ?? "Walk-in Guest",
                        MachineId = machineId
                    };

                    await _repository.InsertSessionAsync(newSession);
                    return "Success: Checked in!";
                }
                else
                {
                    // Check-out: complete session, calculate fee, generate receipt
                    var session = activeSessionResponse;
                    session.EndTime = DateTime.UtcNow;
                    session.Status = "Completed";
                    session.TotalDurationMinutes = (int)Math.Ceiling((session.EndTime.Value - session.StartTime).TotalMinutes);
                    session.Fee = session.TotalDurationMinutes * _ratePerMinute;
                    session.CheckedOutByStaff = staffName ?? "Unknown";

                    await _repository.UpdateSessionAsync(session);

                    // DEV-8: Auto-generate digital receipt on checkout
                    string? machineName = null;
                    if (session.MachineId.HasValue)
                    {
                        var machine = await _repository.GetMachineAsync(session.MachineId.Value);
                        machineName = machine?.Name;
                    }
                    await _receiptService.GenerateReceiptAsync(session, machineName);

                    _logger.LogInformation("Check-out completed by staff '{Staff}' for tag '{Tag}' at {Time:u}. Duration: {Duration} min, Fee: LKR {Fee:F2}",
                        session.CheckedOutByStaff, tagString, session.EndTime.Value, session.TotalDurationMinutes, session.Fee);
                    return $"Success: Checked out. Duration: {session.TotalDurationMinutes} min | Fee: LKR {session.Fee:F2}";
                }
            }
>>>>>>> Stashed changes
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
