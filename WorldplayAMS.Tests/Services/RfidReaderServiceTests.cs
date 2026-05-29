using System;
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
    public class RfidReaderServiceTests
    {
        private readonly Mock<ISupabaseRepository> _mockRepo;
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<RfidReaderService>> _mockLogger;
        private readonly RfidReaderService _service;

        public RfidReaderServiceTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockLogger = new Mock<ILogger<RfidReaderService>>();
            
            _service = new RfidReaderService(_mockRepo.Object, _cache, _mockLogger.Object);
        }

        [Fact]
        public async Task ValidateTagAsync_ReturnsTag_WhenExistsInDatabase()
        {
            // Arrange
            var tagUid = "TEST-TAG";
            var tagId = Guid.NewGuid();
            var expectedTag = new RfidTag { Id = tagId, TagString = tagUid, Status = "Active" };

            _mockRepo.Setup(r => r.GetTagByStringAsync(tagUid)).ReturnsAsync(expectedTag);

            // Act
            var result = await _service.ValidateTagAsync(tagUid);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedTag);
            
            // Should be cached now
            _cache.TryGetValue($"rfid_{tagUid}", out RfidTag? cachedTag).Should().BeTrue();
            cachedTag.Should().BeEquivalentTo(expectedTag);
        }

        [Fact]
        public async Task ValidateTagAsync_ReturnsNull_WhenNotFoundInDatabase()
        {
            // Arrange
            var tagUid = "NON-EXISTENT-TAG";
            _mockRepo.Setup(r => r.GetTagByStringAsync(tagUid)).ReturnsAsync((RfidTag?)null);

            // Act
            var result = await _service.ValidateTagAsync(tagUid);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ValidateTagAsync_ReturnsFromCache_WhenAlreadyCached()
        {
            // Arrange
            var tagUid = "CACHED-TAG";
            var expectedTag = new RfidTag { Id = Guid.NewGuid(), TagString = tagUid, Status = "Active" };
            
            _cache.Set($"rfid_{tagUid}", expectedTag);

            // Act
            var result = await _service.ValidateTagAsync(tagUid);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedTag);
            
            // Ensure repo was never called
            _mockRepo.Verify(r => r.GetTagByStringAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
