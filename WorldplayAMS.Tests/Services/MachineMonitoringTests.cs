using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Tests.Services
{
    public class MachineMonitoringTests
    {
        private Mock<ISupabaseRepository> _mockRepo;
        private Mock<IFallbackCacheService> _mockCache;
        private Mock<ILogger<MachineMonitoringService>> _mockLogger;
        private MachineMonitoringService _service;

        public MachineMonitoringTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            _mockCache = new Mock<IFallbackCacheService>();
            _mockLogger = new Mock<ILogger<MachineMonitoringService>>();

            _service = new MachineMonitoringService(
                _mockRepo.Object,
                _mockCache.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task ProcessMachineToggleAsync_NoActiveLog_StartsTracking()
        {
            // Arrange
            var machineId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetActiveMachineLogAsync(machineId))
                .ReturnsAsync((MachineUsageLog)null);

            var machine = new ArcadeMachine { Id = machineId, Status = "Online" };
            _mockRepo.Setup(r => r.GetMachineAsync(machineId))
                .ReturnsAsync(machine);

            // Act
            var result = await _service.ProcessMachineToggleAsync(machineId);

            // Assert
            result.Should().Be("Success: Tracking started.");
            
            _mockRepo.Verify(r => r.InsertMachineLogAsync(It.IsAny<MachineUsageLog>()), Times.Once);
            _mockRepo.Verify(r => r.UpdateMachineAsync(It.Is<ArcadeMachine>(m => m.Status == "InUse")), Times.Once);
        }

        [Fact]
        public async Task ProcessMachineToggleAsync_ActiveLogExists_StopsTracking_UpdatesDuration()
        {
            // Arrange
            var machineId = Guid.NewGuid();
            var startTime = DateTime.UtcNow.AddMinutes(-30);
            
            var existingLog = new MachineUsageLog 
            { 
                Id = Guid.NewGuid(), 
                MachineId = machineId, 
                StartTime = startTime, 
                Status = "Active" 
            };

            _mockRepo.Setup(r => r.GetActiveMachineLogAsync(machineId))
                .ReturnsAsync(existingLog);

            var machine = new ArcadeMachine { Id = machineId, Status = "InUse" };
            _mockRepo.Setup(r => r.GetMachineAsync(machineId))
                .ReturnsAsync(machine);

            // Act
            var result = await _service.ProcessMachineToggleAsync(machineId);

            // Assert
            result.Should().Contain("Tracking stopped");
            result.Should().Contain("Duration: 30 min");

            _mockRepo.Verify(r => r.UpdateMachineLogAsync(It.Is<MachineUsageLog>(
                l => l.Status == "Completed" && l.DurationMinutes == 30
            )), Times.Once);

            _mockRepo.Verify(r => r.UpdateMachineAsync(It.Is<ArcadeMachine>(m => m.Status == "Online")), Times.Once);
        }

        [Fact]
        public async Task ProcessMachineToggleAsync_OfflineException_FallsBackToCache()
        {
            // Arrange
            var machineId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetActiveMachineLogAsync(machineId))
                .ThrowsAsync(new Exception("Network failure"));

            // Act
            var result = await _service.ProcessMachineToggleAsync(machineId);

            // Assert
            result.Should().Be("Offline: Tap recorded locally. Will sync when online.");
            
            _mockCache.Verify(c => c.SaveFailedSession($"Machine_{machineId}", "ToggleMachineLog"), Times.Once);
        }
    }
}
