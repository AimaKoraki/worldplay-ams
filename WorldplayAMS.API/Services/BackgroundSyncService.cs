using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WorldplayAMS.Core.Interfaces;

namespace WorldplayAMS.API.Services;

/// <summary>
/// Background worker that periodically retries failed RFID taps and machine toggles
/// that were cached by FallbackCacheService when Supabase was unreachable.
/// Runs every 5 minutes. Successfully synced payloads are removed from the queue.
/// </summary>
public class BackgroundSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundSyncService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);

    public BackgroundSyncService(IServiceProvider serviceProvider, ILogger<BackgroundSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundSyncService started. Polling every {Interval} minutes.", _syncInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_syncInterval, stoppingToken);

            try
            {
                await SyncPendingPayloadsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in BackgroundSyncService loop.");
            }
        }

        _logger.LogInformation("BackgroundSyncService stopped.");
    }

    private async Task SyncPendingPayloadsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IFallbackCacheService>();

        var pending = cache.GetPendingPayloads();
        if (!pending.Any()) return;

        _logger.LogInformation("Found {Count} offline payload(s) to sync.", pending.Count);

        var sessionManager = scope.ServiceProvider.GetRequiredService<SessionManagerService>();
        var machineService = scope.ServiceProvider.GetRequiredService<MachineMonitoringService>();

        foreach (var payload in pending)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                switch (payload.ActionType)
                {
                    case "CheckInOutTap":
                        var tapResult = await sessionManager.ProcessRfidTapAsync(payload.TagString);
                        // Only remove if the operation succeeded (not another offline failure)
                        if (!tapResult.StartsWith("Offline"))
                        {
                            cache.RemovePayload(payload.Id);
                            _logger.LogInformation("Synced RFID tap payload {Id}: {Result}", payload.Id, tapResult);
                        }
                        break;

                    case "ToggleMachineLog":
                        // TagString contains "Machine_{Guid}" format for machine payloads
                        var machineIdStr = payload.TagString.Replace("Machine_", "");
                        if (Guid.TryParse(machineIdStr, out var machineId))
                        {
                            var toggleResult = await machineService.ProcessMachineToggleAsync(machineId);
                            if (!toggleResult.StartsWith("Offline"))
                            {
                                cache.RemovePayload(payload.Id);
                                _logger.LogInformation("Synced machine toggle payload {Id}: {Result}", payload.Id, toggleResult);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Invalid machine ID in payload {Id}: {Tag}. Removing stale entry.", payload.Id, payload.TagString);
                            cache.RemovePayload(payload.Id);
                        }
                        break;

                    default:
                        _logger.LogWarning("Unknown action type '{Action}' in payload {Id}. Removing.", payload.ActionType, payload.Id);
                        cache.RemovePayload(payload.Id);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync payload {Id}. Will retry next cycle.", payload.Id);
            }
        }
    }
}
