using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;
using WorldplayAMS.Core.Interfaces;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace WorldplayAMS.API.Services;

    public class MachineMonitoringService
    {
        private readonly ISupabaseRepository _repository;
        private readonly IFallbackCacheService _fallbackCache;
        private readonly ILogger<MachineMonitoringService> _logger;

        public MachineMonitoringService(ISupabaseRepository repository, IFallbackCacheService fallbackCache, ILogger<MachineMonitoringService> logger)
        {
            _repository = repository;
            _fallbackCache = fallbackCache;
            _logger = logger;
        }

        public async Task<string> ProcessMachineToggleAsync(Guid machineId)
        {
            try
            {
                var activeLogResponse = await _repository.GetActiveMachineLogAsync(machineId);

                if (activeLogResponse == null)
                {
                    var newLog = new MachineUsageLog
                    {
                        Id = Guid.NewGuid(),
                        MachineId = machineId,
                        StartTime = DateTime.UtcNow,
                        Status = "Active"
                    };
                    
                    await _repository.InsertMachineLogAsync(newLog);
                    
                    var machine = await _repository.GetMachineAsync(machineId);
                    if (machine != null) {
                        machine.Status = "InUse";
                        await _repository.UpdateMachineAsync(machine);
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

                    await _repository.UpdateMachineLogAsync(log);

                    var machine = await _repository.GetMachineAsync(machineId);
                    if (machine != null) {
                        machine.Status = "Online";
                        await _repository.UpdateMachineAsync(machine);
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
            return await _repository.GetAllMachinesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get machines");
            return new List<ArcadeMachine>();
        }
    }
    public async Task<List<MachineUsageLog>> GetUsageLogsAsync()
    {
        try
        {
            return await _repository.GetAllMachineUsageLogsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get machine usage logs");
            return new List<MachineUsageLog>();
        }
    }
}
