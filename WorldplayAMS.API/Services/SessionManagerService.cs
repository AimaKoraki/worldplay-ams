using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;
using WorldplayAMS.Core.Interfaces;namespace WorldplayAMS.API.Services;

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

        public async Task<string> ProcessRfidTapAsync(string tagString, string? staffName = null, string? guestName = null, Guid? machineId = null, Guid? staffId = null)
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

                    // DEV-17: Audit log — Check-in
                    await _repository.InsertAuditLogAsync(new ManagerAuditLog
                    {
                        Id = Guid.NewGuid(),
                        ManagerId = staffId,  // null when no staff authenticated — avoids FK violation with Guid.Empty
                        ManagerName = staffName ?? "Unknown",
                        Action = "SESSION_CHECK_IN",
                        Details = $"Tag: {tagString} | Guest: {guestName ?? "Walk-in Guest"} | Machine: {machineId?.ToString() ?? "None"}",
                        Timestamp = DateTime.UtcNow
                    });

                    return "Success: Checked in!";
                }
                else
                {
                    // Check-out: complete session, calculate fee, generate receipt
                    var session = activeSessionResponse;
                    var endTime = DateTime.UtcNow;
                    session.EndTime = endTime;
                    session.Status = "Completed";

                    if (session.StartTime.Kind != DateTimeKind.Utc)
                        _logger.LogWarning("StartTime returned from DB with Kind={Kind} (value={Value}). Converting to UTC.", session.StartTime.Kind, session.StartTime);
                    
                    // Supabase Postgres often returns timestamp without timezone as Unspecified but it represents Local time.
                    // By specifying it as Local first, ToUniversalTime() correctly shifts it back to the original UTC value.
                    var startUtc = session.StartTime.Kind == DateTimeKind.Unspecified 
                        ? DateTime.SpecifyKind(session.StartTime, DateTimeKind.Local).ToUniversalTime()
                        : session.StartTime.ToUniversalTime();

                    var durationMinutes = (endTime - startUtc).TotalMinutes;
                    var durationSeconds = (int)Math.Round((endTime - startUtc).TotalSeconds);
                    
                    // Failsafe: prevent negative durations if DB data is corrupted
                    if (durationMinutes < 0) durationMinutes = 0;
                    if (durationSeconds < 0) durationSeconds = 0;

                    // Ceiling for billing, but no artificial 1-min floor
                    session.TotalDurationMinutes = (int)Math.Ceiling(durationMinutes);
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

                    // DEV-17: Audit log — Check-out with billing
                    await _repository.InsertAuditLogAsync(new ManagerAuditLog
                    {
                        Id = Guid.NewGuid(),
                        ManagerId = staffId,  // null when no staff authenticated — avoids FK violation with Guid.Empty
                        ManagerName = staffName ?? "Unknown",
                        Action = "SESSION_CHECK_OUT",
                        Details = $"Tag: {tagString} | Guest: {session.GuestName} | Duration: {session.TotalDurationMinutes} min | Fee: LKR {session.Fee:F2} | Staff: {session.CheckedOutByStaff}",
                        Timestamp = DateTime.UtcNow
                    });

                    // Format duration as MM:SS for the response message
                    var displayMins = durationSeconds / 60;
                    var displaySecs = durationSeconds % 60;
                    var durationDisplay = displaySecs > 0
                        ? $"{displayMins} min {displaySecs} sec"
                        : $"{displayMins} min";

                    _logger.LogInformation("Check-out completed by staff '{Staff}' for tag '{Tag}' at {Time:u}. Duration: {Duration} min, Fee: LKR {Fee:F2}",
                        session.CheckedOutByStaff, tagString, session.EndTime.Value, session.TotalDurationMinutes, session.Fee);
                    return $"Success: Checked out. Duration: {durationDisplay} | Fee: LKR {session.Fee:F2}";
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
                return await _repository.GetActiveSessionsAsync();
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
                return await _repository.GetCompletedSessionsAsync();
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
