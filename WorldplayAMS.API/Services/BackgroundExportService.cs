using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services
{
    public class ExportJobStateTracker
    {
        private readonly ConcurrentDictionary<Guid, ExportJobStatus> _jobs = new();

        public void AddJob(ExportJobStatus status) => _jobs[status.JobId] = status;
        
        public ExportJobStatus? GetJob(Guid jobId) => _jobs.TryGetValue(jobId, out var status) ? status : null;
        
        public void UpdateJob(Guid jobId, Action<ExportJobStatus> updateAction)
        {
            if (_jobs.TryGetValue(jobId, out var status))
            {
                updateAction(status);
            }
        }
    }

    public class ExportJobQueue
    {
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
        
        public async Task QueueJobAsync(Guid jobId) => await _queue.Writer.WriteAsync(jobId);
        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken token) => _queue.Reader.ReadAllAsync(token);
    }

    public class BackgroundExportService : BackgroundService
    {
        private readonly ExportJobQueue _queue;
        private readonly ExportJobStateTracker _tracker;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundExportService> _logger;

        public BackgroundExportService(
            ExportJobQueue queue,
            ExportJobStateTracker tracker,
            IServiceProvider serviceProvider,
            ILogger<BackgroundExportService> logger)
        {
            _queue = queue;
            _tracker = tracker;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var jobId in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var exportDataService = scope.ServiceProvider.GetRequiredService<ExportDataService>();
                    
                    _tracker.UpdateJob(jobId, s => s.Status = "Processing");
                    
                    await exportDataService.ProcessJobAsync(jobId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing export job {JobId}", jobId);
                    _tracker.UpdateJob(jobId, s => 
                    {
                        s.Status = "Failed";
                        s.ErrorMessage = ex.Message;
                    });
                }
            }
        }
    }
}
