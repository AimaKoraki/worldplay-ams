using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Models;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using WorldplayAMS.Core.Interfaces;

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

    public async Task<string> ProcessMachineToggleAsync(Guid machineId, string technicianName = "Unknown Technician", Guid? staffId = null)
    {
        try
        {
            var activeLogResponse = await _repository.GetActiveMachineLogAsync(machineId);
            var machine = await _repository.GetMachineAsync(machineId);
            var machineName = machine?.Name ?? "Unknown Machine";

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
                
                if (machine != null) {
                    machine.Status = "InUse";
                    await _repository.UpdateMachineAsync(machine);
                }

                // DEV-07: Log audit trail for starting a machine session
                await _repository.InsertAuditLogAsync(new ManagerAuditLog
                {
                    Id = Guid.NewGuid(),
                    ManagerId = staffId,
                    ManagerName = technicianName,
                    Action = "StartMachineSession",
                    Details = $"Started session on machine: {machineName} ({machineId})"
                });

                return "Success: Tracking started.";
            }
            else
            {
                var log = activeLogResponse;
                log.EndTime = DateTime.UtcNow;
                log.Status = "Completed";
                if (log.EndTime.HasValue) 
                {
                    // Fix: Use Math.Ceiling to match session billing logic
                    log.DurationMinutes = (int)Math.Ceiling((log.EndTime.Value - log.StartTime).TotalMinutes);
                }

                await _repository.UpdateMachineLogAsync(log);

                if (machine != null) {
                    machine.Status = "Online";
                    await _repository.UpdateMachineAsync(machine);
                }

                // DEV-07: Log audit trail for stopping a machine session
                await _repository.InsertAuditLogAsync(new ManagerAuditLog
                {
                    Id = Guid.NewGuid(),
                    ManagerId = staffId,
                    ManagerName = technicianName,
                    Action = "StopMachineSession",
                    Details = $"Stopped session on machine: {machineName} ({machineId}). Duration: {log.DurationMinutes} min."
                });

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
