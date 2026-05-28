using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Tests.Services
{
    public class GameSessionServiceTests
    {
        private readonly Mock<ISupabaseRepository> _mockRepo;
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<GameSessionService>> _mockLogger;
        private readonly GameSessionService _service;

        public GameSessionServiceTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockLogger = new Mock<ILogger<GameSessionService>>();
            
            _service = new GameSessionService(_mockRepo.Object, _cache, _mockLogger.Object);
        }

        [Fact]
        public async Task StartSessionAsync_ReturnsNull_WhenTagIsInvalid()
        {
            // Arrange
            var tagUid = "INVALID-TAG";
            var machineId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetActiveTagAsync(tagUid)).ReturnsAsync((RfidTag)null);

            // Act
            var result = await _service.StartSessionAsync(tagUid, machineId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task StartSessionAsync_ReturnsExistingActiveSession_WhenOneExists()
        {
            // Arrange
            var tagUid = "VALID-TAG";
            var machineId = Guid.NewGuid();
            var tagId = Guid.NewGuid();
            var existingSession = new Session { Id = Guid.NewGuid(), RfidTagId = tagId, Status = "Active" };

            _mockRepo.Setup(r => r.GetActiveTagAsync(tagUid))
                .ReturnsAsync(new RfidTag { Id = tagId, TagString = tagUid, Status = "Active" });
            
            _mockRepo.Setup(r => r.GetActiveSessionAsync(tagId))
                .ReturnsAsync(existingSession);

            // Act
            var result = await _service.StartSessionAsync(tagUid, machineId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(existingSession.Id);
            _mockRepo.Verify(r => r.InsertSessionAsync(It.IsAny<Session>()), Times.Never);
        }

        [Fact]
        public async Task StartSessionAsync_CreatesNewSession_WhenNoneExists()
        {
            // Arrange
            var tagUid = "VALID-TAG";
            var machineId = Guid.NewGuid();
            var tagId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetActiveTagAsync(tagUid))
                .ReturnsAsync(new RfidTag { Id = tagId, TagString = tagUid, Status = "Active" });
            
            _mockRepo.Setup(r => r.GetActiveSessionAsync(tagId))
                .ReturnsAsync((Session)null);

            // Act
            var result = await _service.StartSessionAsync(tagUid, machineId);

            // Assert
            result.Should().NotBeNull();
            result.MachineId.Should().Be(machineId);
            result.RfidTagId.Should().Be(tagId);
            result.Status.Should().Be("Active");
            
            _mockRepo.Verify(r => r.InsertSessionAsync(It.IsAny<Session>()), Times.Once);
        }

        [Fact]
        public async Task GetActiveSessionsAsync_ReturnsDataFromRepository()
        {
            // Arrange
            var expectedSessions = new List<Session>
            {
                new Session { Id = Guid.NewGuid(), Status = "Active" }
            };

            _mockRepo.Setup(r => r.GetActiveSessionsAsync()).ReturnsAsync(expectedSessions);

            // Act
            var result = await _service.GetActiveSessionsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedSessions);
        }
    }
}
