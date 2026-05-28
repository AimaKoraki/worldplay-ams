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
    }
}
