using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;
using Xunit;

namespace WorldplayAMS.Tests.Services
{
    public class DigitalReceiptServiceTests
    {
        private readonly Mock<ISupabaseRepository> _mockRepo;
        private readonly Mock<ILogger<DigitalReceiptService>> _mockLogger;
        private readonly DigitalReceiptService _service;

        public DigitalReceiptServiceTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            _mockLogger = new Mock<ILogger<DigitalReceiptService>>();
            _service = new DigitalReceiptService(_mockRepo.Object, _mockLogger.Object);
        }

        // ── DEV-8: GenerateReceiptAsync — Happy Path ─────────────────────────

        [Fact]
        public async Task GenerateReceiptAsync_CompletedSession_ReturnsReceiptWithCorrectFields()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var tagId = Guid.NewGuid();
            var checkIn = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
            var checkOut = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc);

            var session = new Session
            {
                Id = sessionId,
                RfidTagId = tagId,
                StartTime = checkIn,
                EndTime = checkOut,
                Status = "Completed",
                TotalDurationMinutes = 60,
                Fee = 9.00m,
                GuestName = "Test Guest",
                CheckedOutByStaff = "Staff01"
            };

            _mockRepo
                .Setup(r => r.GetReceiptBySessionIdAsync(sessionId))
                .ReturnsAsync((DigitalReceipt?)null); // no existing receipt

            DigitalReceipt? capturedReceipt = null;
            _mockRepo
                .Setup(r => r.InsertReceiptAsync(It.IsAny<DigitalReceipt>()))
                .Callback<DigitalReceipt>(r => capturedReceipt = r)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GenerateReceiptAsync(session, "Cyber Racer");

            // Assert
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(sessionId);
            result.RfidTagId.Should().Be(tagId);
            result.GuestName.Should().Be("Test Guest");
            result.MachineName.Should().Be("Cyber Racer");
            result.Fee.Should().Be(9.00m);
            result.DurationMinutes.Should().Be(60);
            result.StaffName.Should().Be("Staff01");
            result.Status.Should().Be("Issued");
        }

        [Fact]
        public async Task GenerateReceiptAsync_ReceiptNumber_MatchesExpectedFormat()
        {
            // Arrange — format must be WP-YYYYMMDD-XXXX
            var sessionId = Guid.NewGuid();
            var checkOut = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc);

            var session = new Session
            {
                Id = sessionId,
                RfidTagId = Guid.NewGuid(),
                StartTime = checkOut.AddHours(-1),
                EndTime = checkOut,
                Status = "Completed",
                TotalDurationMinutes = 60,
                Fee = 9.00m,
                GuestName = "Test Guest",
                CheckedOutByStaff = "Staff01"
            };

            _mockRepo.Setup(r => r.GetReceiptBySessionIdAsync(sessionId)).ReturnsAsync((DigitalReceipt?)null);
            _mockRepo.Setup(r => r.InsertReceiptAsync(It.IsAny<DigitalReceipt>())).Returns(Task.CompletedTask);

            // Act
            var result = await _service.GenerateReceiptAsync(session);

            // Assert — WP-20260501-XXXX
            result.Should().NotBeNull();
            result!.ReceiptNumber.Should().MatchRegex(@"^WP-\d{8}-[A-F0-9]{4}$");
            result.ReceiptNumber.Should().StartWith("WP-20260501-");
        }

        // ── DEV-8: Idempotency Guard ─────────────────────────────────────────

        [Fact]
        public async Task GenerateReceiptAsync_ReceiptAlreadyExists_ReturnsExistingWithoutInsert()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var existingReceipt = new DigitalReceipt
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ReceiptNumber = "WP-20260501-ABCD",
                Status = "Issued"
            };

            var session = new Session
            {
                Id = sessionId,
                RfidTagId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow,
                Status = "Completed",
                TotalDurationMinutes = 60,
                Fee = 9.00m,
                GuestName = "Test Guest"
            };

            _mockRepo
                .Setup(r => r.GetReceiptBySessionIdAsync(sessionId))
                .ReturnsAsync(existingReceipt); // already exists

            // Act
            var result = await _service.GenerateReceiptAsync(session);

            // Assert — returns existing, never inserts a duplicate
            result.Should().NotBeNull();
            result!.ReceiptNumber.Should().Be("WP-20260501-ABCD");
            _mockRepo.Verify(r => r.InsertReceiptAsync(It.IsAny<DigitalReceipt>()), Times.Never);
        }

        // ── DEV-8: Guard Clauses — Non-completed sessions ────────────────────

        [Fact]
        public async Task GenerateReceiptAsync_ActiveSession_ReturnsNull()
        {
            // Arrange — active session (not checked out yet)
            var session = new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = null, // still active
                Status = "Active",
                Fee = null
            };

            // Act
            var result = await _service.GenerateReceiptAsync(session);

            // Assert — must not generate receipt for active sessions
            result.Should().BeNull();
            _mockRepo.Verify(r => r.InsertReceiptAsync(It.IsAny<DigitalReceipt>()), Times.Never);
        }

        [Fact]
        public async Task GenerateReceiptAsync_MissingFee_ReturnsNull()
        {
            // Arrange — completed status but fee not calculated (data integrity issue)
            var session = new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow,
                Status = "Completed",
                Fee = null // fee missing
            };

            var result = await _service.GenerateReceiptAsync(session);

            result.Should().BeNull();
            _mockRepo.Verify(r => r.InsertReceiptAsync(It.IsAny<DigitalReceipt>()), Times.Never);
        }

        // ── DEV-8: Repository Resilience ─────────────────────────────────────

        [Fact]
        public async Task GenerateReceiptAsync_RepositoryThrows_ReturnsNull()
        {
            // Arrange
            var session = new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow,
                Status = "Completed",
                TotalDurationMinutes = 60,
                Fee = 9.00m,
                GuestName = "Test Guest"
            };

            _mockRepo
                .Setup(r => r.GetReceiptBySessionIdAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("Supabase unreachable"));

            // Act
            var result = await _service.GenerateReceiptAsync(session);

            // Assert — must return null, not throw
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetReceiptsByDateRangeAsync_ReturnsEmptyList_WhenRepositoryThrows()
        {
            // Arrange
            _mockRepo
                .Setup(r => r.GetReceiptsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("DB unavailable"));

            // Act
            var result = await _service.GetReceiptsByDateRangeAsync(DateTime.UtcNow.Date, DateTime.UtcNow);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // ── DEV-8: SearchReceiptsAsync ───────────────────────────────────────

        [Fact]
        public async Task SearchReceiptsAsync_ExactReceiptNumber_ReturnsMatchWithoutFallback()
        {
            // Arrange
            var exactReceipt = new DigitalReceipt { Id = Guid.NewGuid(), ReceiptNumber = "WP-20260501-ABCD", GuestName = "Test" };

            _mockRepo
                .Setup(r => r.GetReceiptByNumberAsync("WP-20260501-ABCD"))
                .ReturnsAsync(exactReceipt);

            // Act
            var result = await _service.SearchReceiptsAsync("WP-20260501-ABCD");

            // Assert — exact match, should NOT fall back to GetReceiptsByDateRangeAsync
            result.Should().HaveCount(1);
            result[0].ReceiptNumber.Should().Be("WP-20260501-ABCD");
            _mockRepo.Verify(r => r.GetReceiptsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task SearchReceiptsAsync_NoExactMatch_FallsBackTo30DayRange()
        {
            // Arrange — no exact match, repo returns list for date range
            _mockRepo
                .Setup(r => r.GetReceiptByNumberAsync(It.IsAny<string>()))
                .ReturnsAsync((DigitalReceipt?)null);

            var recent = new List<DigitalReceipt>
            {
                new DigitalReceipt { Id = Guid.NewGuid(), ReceiptNumber = "WP-20260430-CAFE", GuestName = "Alice", MachineName = "VR Station" },
                new DigitalReceipt { Id = Guid.NewGuid(), ReceiptNumber = "WP-20260429-BEEF", GuestName = "Bob", MachineName = "Cyber Racer" }
            };

            _mockRepo
                .Setup(r => r.GetReceiptsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(recent);

            // Act — search by machine name
            var result = await _service.SearchReceiptsAsync("VR Station");

            // Assert — only Alice's receipt matches the machine name filter
            result.Should().HaveCount(1);
            result[0].GuestName.Should().Be("Alice");
        }
    }
}
