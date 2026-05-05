using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
    public class SessionManagerTests
    {
        private Mock<ISupabaseRepository> _mockRepo;
        private Mock<IFallbackCacheService> _mockCache;
        private Mock<ILogger<SessionManagerService>> _mockLogger;
        private Mock<ILogger<DigitalReceiptService>> _mockReceiptLogger;
        private IConfiguration _configuration;
        private SessionManagerService _service;

        public SessionManagerTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            _mockCache = new Mock<IFallbackCacheService>();
            _mockLogger = new Mock<ILogger<SessionManagerService>>();
            _mockReceiptLogger = new Mock<ILogger<DigitalReceiptService>>();
            
            var inMemorySettings = new Dictionary<string, string> {
                {"Billing:RatePerMinute", "0.15"}
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var receiptService = new DigitalReceiptService(_mockRepo.Object, _mockReceiptLogger.Object);

            _service = new SessionManagerService(
                _mockRepo.Object,
                _mockCache.Object,
                receiptService,
                _mockLogger.Object,
                _configuration
            );
        }

        [Fact]
        public async Task ProcessRfidTapAsync_ValidTag_StartsNewSession_WhenNoActiveSessionExists()
        {
            // Arrange
            var tagString = "DEMO-TAG-001";
            var tagId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetTagByStringAsync(tagString))
                .ReturnsAsync(new RfidTag { Id = tagId, Status = "Active", TagString = tagString });

            // Returning null simulates the missing check-in edge case (no active session found)
            _mockRepo.Setup(r => r.GetActiveSessionAsync(tagId))
                .ReturnsAsync((Session)null);

            // Act
            var result = await _service.ProcessRfidTapAsync(tagString);

            // Assert
            result.Should().Be("Success: Checked in!");
            _mockRepo.Verify(r => r.InsertSessionAsync(It.IsAny<Session>()), Times.Once);
            _mockRepo.Verify(r => r.InsertAuditLogAsync(It.Is<ManagerAuditLog>(log => log.Action == "SESSION_CHECK_IN")), Times.Once);
        }

        [Fact]
        public async Task ProcessRfidTapAsync_ValidTag_ChecksOut_WhenActiveSessionExists()
        {
            // Arrange
            var tagString = "DEMO-TAG-001";
            var tagId = Guid.NewGuid();
            var session = new Session { 
                Id = Guid.NewGuid(), 
                RfidTagId = tagId, 
                StartTime = DateTime.UtcNow.AddMinutes(-10), 
                Status = "Active" 
            };

            _mockRepo.Setup(r => r.GetTagByStringAsync(tagString))
                .ReturnsAsync(new RfidTag { Id = tagId, Status = "Active", TagString = tagString });

            _mockRepo.Setup(r => r.GetActiveSessionAsync(tagId))
                .ReturnsAsync(session);

            // Act
            var result = await _service.ProcessRfidTapAsync(tagString, "Test Staff");

            // Assert
            result.Should().Contain("Success: Checked out.");
            result.Should().Contain("Fee: LKR");
            
            _mockRepo.Verify(r => r.UpdateSessionAsync(It.Is<Session>(s => 
                s.Status == "Completed" 
                && s.TotalDurationMinutes >= 10 
                && s.CheckedOutByStaff == "Test Staff")), Times.Once);
            
            _mockRepo.Verify(r => r.InsertAuditLogAsync(It.Is<ManagerAuditLog>(log => log.Action == "SESSION_CHECK_OUT")), Times.Once);
        }

        [Fact]
        public async Task ProcessRfidTapAsync_UnknownTag_ReturnsError()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetTagByStringAsync(It.IsAny<string>()))
                .ReturnsAsync((RfidTag)null);

            // Act
            var result = await _service.ProcessRfidTapAsync("UNKNOWN-TAG");

            // Assert
            result.Should().Be("Error: RFID tag not found in system.");
        }

        [Fact]
        public async Task ProcessRfidTapAsync_LostTag_ReturnsLostError()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetTagByStringAsync("LOST-TAG-001"))
                .ReturnsAsync(new RfidTag { Id = Guid.NewGuid(), Status = "Lost", TagString = "LOST-TAG-001" });

            // Act
            var result = await _service.ProcessRfidTapAsync("LOST-TAG-001");

            // Assert
            result.Should().Be("Error: This RFID tag has been reported lost. Please contact a manager.");
        }

        // ── DEV-20: Session Logs ─────────────────────────────────────────────

        [Fact]
        public async Task GetActiveSessionsAsync_ReturnsActiveSessions()
        {
            // Arrange
            var expectedSessions = new List<Session>
            {
                new Session { Id = Guid.NewGuid(), Status = "Active" },
                new Session { Id = Guid.NewGuid(), Status = "Active" }
            };

            _mockRepo.Setup(r => r.GetActiveSessionsAsync())
                .ReturnsAsync(expectedSessions);

            // Act
            var result = await _service.GetActiveSessionsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expectedSessions);
        }

        [Fact]
        public async Task GetActiveSessionsAsync_ReturnsEmptyList_WhenRepositoryThrows()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetActiveSessionsAsync())
                .ThrowsAsync(new Exception("DB connection failed"));

            // Act
            var result = await _service.GetActiveSessionsAsync();

            // Assert - silent fallback
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetCompletedSessionsAsync_ReturnsCompletedSessions()
        {
            // Arrange
            var expectedSessions = new List<Session>
            {
                new Session { Id = Guid.NewGuid(), Status = "Completed" },
                new Session { Id = Guid.NewGuid(), Status = "Completed" },
                new Session { Id = Guid.NewGuid(), Status = "Completed" }
            };

            _mockRepo.Setup(r => r.GetCompletedSessionsAsync())
                .ReturnsAsync(expectedSessions);

            // Act
            var result = await _service.GetCompletedSessionsAsync();

            // Assert
            result.Should().HaveCount(3);
            result.Should().BeEquivalentTo(expectedSessions);
        }

        [Fact]
        public async Task GetCompletedSessionsAsync_ReturnsEmptyList_WhenRepositoryThrows()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetCompletedSessionsAsync())
                .ThrowsAsync(new Exception("DB connection failed"));

            // Act
            var result = await _service.GetCompletedSessionsAsync();

            // Assert - silent fallback
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }
}
