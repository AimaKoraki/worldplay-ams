using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace WorldplayAMS.API.Services;

public class MachineMonitoringService
{
    private readonly Supabase.Client _supabase;
    private readonly IFallbackCacheService _fallbackCache;
    private readonly ILogger<MachineMonitoringService> _logger;

    public MachineMonitoringService(Supabase.Client supabase, IFallbackCacheService fallbackCache, ILogger<MachineMonitoringService> logger)
    {
        _supabase = supabase;
        _fallbackCache = fallbackCache;
        _logger = logger;
    }

    public async Task<string> ProcessMachineToggleAsync(Guid machineId)
    {
        try
        {
            var activeLogResponse = await _supabase.From<MachineUsageLog>()
                .Where(m => m.MachineId == machineId && m.Status == "Active")
                .Single();

            if (activeLogResponse == null)
            {
                var newLog = new MachineUsageLog
                {
                    Id = Guid.NewGuid(),
                    MachineId = machineId,
                    StartTime = DateTime.UtcNow,
                    Status = "Active"
                };
                
                await _supabase.From<MachineUsageLog>().Insert(newLog);
                
                var machine = await _supabase.From<ArcadeMachine>().Where(m => m.Id == machineId).Single();
                if (machine != null) {
                    machine.Status = "InUse";
                    await _supabase.From<ArcadeMachine>().Update(machine);
                }

                return "Success: Tracking started.";
            }
            else
            {
                var log = activeLogResponse;
                log.EndTime = DateTime.UtcNow;
                log.Status = "Completed";
                if (log.EndTime.HasValue) 
                {
                    log.DurationMinutes = (int)(log.EndTime.Value - log.StartTime).TotalMinutes;
                }

                await _supabase.From<MachineUsageLog>().Update(log);

                var machine = await _supabase.From<ArcadeMachine>().Where(m => m.Id == machineId).Single();
                if (machine != null) {
                    machine.Status = "Online";
                    await _supabase.From<ArcadeMachine>().Update(machine);
                }

                return $"Success: Tracking stopped. Duration: {log.DurationMinutes} min.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed mapping machine telemetry offline.");
            _fallbackCache.SaveFailedSession($"Machine_{machineId}", "ToggleMachineLog");
            return "Offline: Tap recorded locally. Will sync when online.";
        }
    }

    public async Task<List<ArcadeMachine>> GetAllMachinesAsync()
    {
        try
        {
            var response = await _supabase.From<ArcadeMachine>().Get();
            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get machines");
            return new List<ArcadeMachine>();
        }
    }
}
