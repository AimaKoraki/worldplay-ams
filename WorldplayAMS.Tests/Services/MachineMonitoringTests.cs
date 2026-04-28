using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;
using System.Collections.Generic;

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
        public async Task ProcessMachineToggleAsync_NoActiveLog_StartsTrackingAndLogsAudit()
        {
            // Arrange
            var machineId = Guid.NewGuid();
            var techName = "Test Tech";

            _mockRepo.Setup(r => r.GetActiveMachineLogAsync(machineId))
                .ReturnsAsync((MachineUsageLog?)null);

            _mockRepo.Setup(r => r.GetMachineAsync(machineId))
                .ReturnsAsync(new ArcadeMachine { Id = machineId, Name = "VR Station", Status = "Online" });

            // Act
            var result = await _service.ProcessMachineToggleAsync(machineId, techName);

            // Assert
            result.Should().Be("Success: Tracking started.");
            
            // Verify new log inserted
            _mockRepo.Verify(r => r.InsertMachineLogAsync(It.Is<MachineUsageLog>(l => l.MachineId == machineId && l.Status == "Active")), Times.Once);
            
            // Verify machine status updated
            _mockRepo.Verify(r => r.UpdateMachineAsync(It.Is<ArcadeMachine>(m => m.Status == "InUse")), Times.Once);
            
            // Verify audit log
            _mockRepo.Verify(r => r.InsertAuditLogAsync(It.Is<ManagerAuditLog>(a => 
                a.ManagerName == techName && 
                a.Action == "StartMachineSession")), Times.Once);
        }

        [Fact]
        public async Task ProcessMachineToggleAsync_ActiveLogExists_StopsTrackingAndLogsAudit()
        {
            // Arrange
            var machineId = Guid.NewGuid();
            var techName = "Test Tech";
            var startTime = DateTime.UtcNow.AddMinutes(-10.5); // 10.5 mins

            var activeLog = new MachineUsageLog
            {
                Id = Guid.NewGuid(),
                MachineId = machineId,
                StartTime = startTime,
                Status = "Active"
            };

            _mockRepo.Setup(r => r.GetActiveMachineLogAsync(machineId))
                .ReturnsAsync(activeLog);

            _mockRepo.Setup(r => r.GetMachineAsync(machineId))
                .ReturnsAsync(new ArcadeMachine { Id = machineId, Name = "VR Station", Status = "InUse" });

            // Act
            var result = await _service.ProcessMachineToggleAsync(machineId, techName);

            // Assert
            result.Should().Contain("Success: Tracking stopped.");
            result.Should().Contain("Duration: 11 min"); // Math.Ceiling(10.5) = 11
            
            // Verify log updated with correct duration
            _mockRepo.Verify(r => r.UpdateMachineLogAsync(It.Is<MachineUsageLog>(l => 
                l.Status == "Completed" && 
                l.DurationMinutes == 11)), Times.Once);
            
            // Verify machine status updated
            _mockRepo.Verify(r => r.UpdateMachineAsync(It.Is<ArcadeMachine>(m => m.Status == "Online")), Times.Once);
            
            // Verify audit log
            _mockRepo.Verify(r => r.InsertAuditLogAsync(It.Is<ManagerAuditLog>(a => 
                a.ManagerName == techName && 
                a.Action == "StopMachineSession")), Times.Once);
        }
    }
}
