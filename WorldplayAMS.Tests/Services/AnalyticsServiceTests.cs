using System;
using System.Collections.Generic;
using System.Linq;
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
    public class AnalyticsServiceTests
    {
        private readonly Mock<ISupabaseRepository> _mockRepo;
        private readonly Mock<MachineMonitoringService> _mockMachineService;
        private readonly Mock<ILogger<AnalyticsService>> _mockLogger;
        private readonly AnalyticsService _service;

        public AnalyticsServiceTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            var mockFallback = new Mock<IFallbackCacheService>();
            var mockMachineLogger = new Mock<ILogger<MachineMonitoringService>>();
            
            // Note: MachineMonitoringService is a concrete class but we can mock its virtual/interface methods if it had any.
            // Wait, we can't easily mock concrete classes unless methods are virtual.
            // Let's instantiate it with a mocked repository instead for the tests, or mock the repo it uses.
            _mockMachineService = new Mock<MachineMonitoringService>(_mockRepo.Object, mockFallback.Object, mockMachineLogger.Object);
            
            _mockLogger = new Mock<ILogger<AnalyticsService>>();
            _service = new AnalyticsService(_mockRepo.Object, _mockMachineService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetPeakHoursAsync_AggregatesCorrectly()
        {
            // Arrange
            var from = DateTime.UtcNow.AddDays(-1);
            var to = DateTime.UtcNow;
            
            // Ensure times fall into the same local hour by keeping them very close together
            var t1 = new DateTime(2023, 1, 1, 14, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2023, 1, 1, 14, 10, 0, DateTimeKind.Utc);
            var t3 = new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            
            var sessions = new List<Session>
            {
                new Session { StartTime = t1 },
                new Session { StartTime = t2 },
                new Session { StartTime = t3 }
            };

            var expectedPeakHour = t1.ToLocalTime().Hour;

            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to)).ReturnsAsync(sessions);

            // Act
            var result = await _service.GetPeakHoursAsync(from, to);

            // Assert
            result.Should().NotBeNull();
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            json.Should().Contain($"\"Hour\":{expectedPeakHour}");
            json.Should().Contain("\"SessionCount\":2");
        }

        [Fact]
        public async Task GetStaffingRecommendationsAsync_CalculatesProperly()
        {
            // Arrange
            var from = DateTime.UtcNow.AddDays(-14);
            var to = DateTime.UtcNow;
            
            var sessions = new List<Session>();
            for(int i=0; i < 10; i++) 
            {
                // 10 sessions on a specific day/hour over 2 weeks = ~5 average
                sessions.Add(new Session { StartTime = new DateTime(2023, 1, 2, 14, 0, 0, DateTimeKind.Utc) }); // Jan 2, 2023 was a Monday
            }

            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to)).ReturnsAsync(sessions);

            // Act
            var result = await _service.GetStaffingRecommendationsAsync(from, to);

            // Assert
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            // 5 avg sessions should recommend 3 staff (base 2 + 1 for 5 sessions)
            json.Should().Contain("\"AverageSessions\":5");
            json.Should().Contain("\"RecommendedStaff\":3");
        }

        [Fact]
        public async Task GetMachineUsageAnalyticsAsync_ReturnsCorrectAggregation()
        {
            // Arrange
            var from = DateTime.UtcNow.AddDays(-1);
            var to = DateTime.UtcNow;
            var machineId = Guid.NewGuid();
            var sessions = new List<Session>
            {
                new Session { MachineId = machineId, TotalDurationMinutes = 30, Fee = 15.0m },
                new Session { MachineId = machineId, TotalDurationMinutes = 20, Fee = 10.0m }
            };
            
            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to)).ReturnsAsync(sessions);
            _mockRepo.Setup(r => r.GetAllMachinesAsync()).ReturnsAsync(new List<ArcadeMachine>
            {
                new ArcadeMachine { Id = machineId, Name = "Test Machine" }
            });

            // Act
            var result = await _service.GetMachineUsageAnalyticsAsync(from, to);

            // Assert
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            json.Should().Contain("\"TotalSessions\":2");
            json.Should().Contain("\"TotalDurationMinutes\":50");
            json.Should().Contain("\"TotalRevenue\":25.0");
        }

        [Fact]
        public async Task GetRevPAMHAsync_CalculatesSystemRevPAMHCorrectly()
        {
            // Arrange
            var from = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2023, 6, 3, 0, 0, 0, DateTimeKind.Utc); // 48 hours

            var machineAId = Guid.NewGuid();
            var machineBId = Guid.NewGuid();

            var sessions = new List<Session>
            {
                new Session { MachineId = machineAId, Fee = 500m, StartTime = from.AddHours(1) },
                new Session { MachineId = machineBId, Fee = 460m, StartTime = from.AddHours(2) }
            };

            var machines = new List<ArcadeMachine>
            {
                new ArcadeMachine { Id = machineAId, Name = "Machine A", Status = "Online" },
                new ArcadeMachine { Id = machineBId, Name = "Machine B", Status = "Online" }
            };

            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to)).ReturnsAsync(sessions);
            _mockRepo.Setup(r => r.GetAllMachinesAsync()).ReturnsAsync(machines);

            // Act
            var result = await _service.GetRevPAMHAsync(from, to);

            // Assert
            result.Should().NotBeNull();
            result!.ActiveMachineCount.Should().Be(2);
            result.TotalHours.Should().Be(48);
            result.TotalRevenue.Should().Be(960m);
            // systemRevPAMH = 960 / (2 * 48) = 10.0
            result.SystemRevPAMH.Should().Be(10.0m);
        }

        [Fact]
        public async Task GetRevPAMHAsync_CalculatesPerMachineRevPAMH()
        {
            // Arrange
            var from = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2023, 6, 2, 0, 0, 0, DateTimeKind.Utc); // 24 hours

            var machineAId = Guid.NewGuid();
            var machineBId = Guid.NewGuid();

            var sessions = new List<Session>
            {
                new Session { MachineId = machineAId, Fee = 200m, StartTime = from.AddHours(1) },
                new Session { MachineId = machineAId, Fee = 100m, StartTime = from.AddHours(5) },
                new Session { MachineId = machineBId, Fee = 150m, StartTime = from.AddHours(3) }
            };

            var machines = new List<ArcadeMachine>
            {
                new ArcadeMachine { Id = machineAId, Name = "Machine A", Status = "Online" },
                new ArcadeMachine { Id = machineBId, Name = "Machine B", Status = "Online" }
            };

            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to)).ReturnsAsync(sessions);
            _mockRepo.Setup(r => r.GetAllMachinesAsync()).ReturnsAsync(machines);

            // Act
            var result = await _service.GetRevPAMHAsync(from, to);

            // Assert
            result.Should().NotBeNull();
            // Machine A: revenue = 300, RevPAMH = 300 / 24 = 12.5
            var machineARevPAMH = result!.MachineRevPAMH.FirstOrDefault(m => m.MachineId == machineAId);
            machineARevPAMH.Should().NotBeNull();
            machineARevPAMH!.TotalRevenue.Should().Be(300m);
            machineARevPAMH.RevPAMH.Should().Be(12.5m);

            // Machine B: revenue = 150, RevPAMH = 150 / 24 = 6.25
            var machineBRevPAMH = result.MachineRevPAMH.FirstOrDefault(m => m.MachineId == machineBId);
            machineBRevPAMH.Should().NotBeNull();
            machineBRevPAMH!.TotalRevenue.Should().Be(150m);
            machineBRevPAMH.RevPAMH.Should().Be(6.25m);
        }

        [Fact]
        public async Task GetRevPAMHAsync_ReturnsNull_WhenExceptionOccurs()
        {
            // Arrange
            var from = DateTime.UtcNow.AddDays(-1);
            var to = DateTime.UtcNow;

            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _service.GetRevPAMHAsync(from, to);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRevPAMHAsync_HandlesZeroMachinesGracefully()
        {
            // Arrange
            var from = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2023, 6, 2, 0, 0, 0, DateTimeKind.Utc);

            var sessions = new List<Session>
            {
                new Session { MachineId = Guid.NewGuid(), Fee = 100m, StartTime = from.AddHours(1) }
            };

            var machines = new List<ArcadeMachine>
            {
                new ArcadeMachine { Id = Guid.NewGuid(), Name = "Offline Machine", Status = "Offline" }
            };

            _mockRepo.Setup(r => r.GetSessionsByDateRangeAsync(from, to)).ReturnsAsync(sessions);
            _mockRepo.Setup(r => r.GetAllMachinesAsync()).ReturnsAsync(machines);

            // Act
            var result = await _service.GetRevPAMHAsync(from, to);

            // Assert
            result.Should().NotBeNull();
            result!.ActiveMachineCount.Should().Be(0);
            result.SystemRevPAMH.Should().Be(0m);
        }
    }
}
