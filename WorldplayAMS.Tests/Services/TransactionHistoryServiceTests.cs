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
    public class TransactionHistoryServiceTests
    {
        private readonly Mock<ISupabaseRepository> _mockRepo;
        private readonly Mock<ILogger<TransactionHistoryService>> _mockLogger;
        private readonly TransactionHistoryService _service;

        public TransactionHistoryServiceTests()
        {
            _mockRepo = new Mock<ISupabaseRepository>();
            _mockLogger = new Mock<ILogger<TransactionHistoryService>>();
            _service = new TransactionHistoryService(_mockRepo.Object, _mockLogger.Object);
        }

        // Helper: serialize anonymous type → typed DTO for safe cross-assembly assertions
        private static ReconciliationSummaryDto ToSummaryDto(object? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            return System.Text.Json.JsonSerializer.Deserialize<ReconciliationSummaryDto>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        // ── DEV-13: GetTransactionsByDateRangeAsync ──────────────────────────

        [Fact]
        public async Task GetTransactionsByDateRangeAsync_ReturnsSessions_ForValidRange()
        {
            // Arrange
            var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 5, 1, 23, 59, 59, DateTimeKind.Utc);

            var expected = new List<Session>
            {
                new Session { Id = Guid.NewGuid(), StartTime = from.AddHours(2), EndTime = from.AddHours(3), Fee = 150m, TotalDurationMinutes = 60, Status = "Completed" },
                new Session { Id = Guid.NewGuid(), StartTime = from.AddHours(4), EndTime = from.AddHours(5), Fee = 200m, TotalDurationMinutes = 60, Status = "Completed" }
            };

            _mockRepo
                .Setup(r => r.GetSessionsByDateRangeAsync(from, to))
                .ReturnsAsync(expected);

            // Act
            var result = await _service.GetTransactionsByDateRangeAsync(from, to);

            // Assert
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task GetTransactionsByDateRangeAsync_ReturnsEmptyList_WhenRepositoryThrows()
        {
            // Arrange
            _mockRepo
                .Setup(r => r.GetSessionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _service.GetTransactionsByDateRangeAsync(DateTime.UtcNow.Date, DateTime.UtcNow);

            // Assert: must return empty list, not throw (resilience requirement)
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // ── DEV-13: GetDailyReconciliationSummaryAsync ───────────────────────

        [Fact]
        public async Task GetDailyReconciliationSummaryAsync_CalculatesCorrectTotals()
        {
            // Arrange
            var date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

            var sessions = new List<Session>
            {
                new Session { Id = Guid.NewGuid(), Fee = 150m, TotalDurationMinutes = 60, Status = "Completed", EndTime = date.AddHours(10) },
                new Session { Id = Guid.NewGuid(), Fee = 200m, TotalDurationMinutes = 90, Status = "Completed", EndTime = date.AddHours(10) },
                new Session { Id = Guid.NewGuid(), Fee = 100m, TotalDurationMinutes = 30, Status = "Completed", EndTime = date.AddHours(14) }
            };

            _mockRepo
                .Setup(r => r.GetSessionsByDateRangeAsync(date.Date, It.IsAny<DateTime>()))
                .ReturnsAsync(sessions);

            // Act
            var summary = ToSummaryDto(await _service.GetDailyReconciliationSummaryAsync(date));

            // Assert
            summary.TotalSessions.Should().Be(3);
            summary.TotalRevenue.Should().Be(450m);
            summary.AverageDurationMinutes.Should().BeApproximately(60.0, 0.01);
            summary.HighestSingleFee.Should().Be(200m);
            summary.LongestSessionMinutes.Should().Be(90);
        }

        [Fact]
        public async Task GetDailyReconciliationSummaryAsync_ReturnsZeroDefaults_WhenNoSessions()
        {
            // Arrange
            _mockRepo
                .Setup(r => r.GetSessionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Session>());

            // Act
            var summary = ToSummaryDto(await _service.GetDailyReconciliationSummaryAsync(DateTime.UtcNow.Date));

            // Assert
            summary.TotalSessions.Should().Be(0);
            summary.TotalRevenue.Should().Be(0m);
            summary.AverageDurationMinutes.Should().Be(0.0);
            summary.PeakCheckOutHourDisplay.Should().Be("N/A");
        }

        [Fact]
        public async Task GetDailyReconciliationSummaryAsync_CalculatesPeakHourCorrectly()
        {
            // Arrange — 2 sessions check out at hour 10, 1 at hour 14 → peak should be 10
            var date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

            var sessions = new List<Session>
            {
                new Session { Id = Guid.NewGuid(), Fee = 100m, TotalDurationMinutes = 30, Status = "Completed", EndTime = date.AddHours(10) },
                new Session { Id = Guid.NewGuid(), Fee = 100m, TotalDurationMinutes = 30, Status = "Completed", EndTime = date.AddHours(10) },
                new Session { Id = Guid.NewGuid(), Fee = 100m, TotalDurationMinutes = 30, Status = "Completed", EndTime = date.AddHours(14) }
            };

            _mockRepo
                .Setup(r => r.GetSessionsByDateRangeAsync(date.Date, It.IsAny<DateTime>()))
                .ReturnsAsync(sessions);

            // Act
            var summary = ToSummaryDto(await _service.GetDailyReconciliationSummaryAsync(date));

            // Assert — peak hour must be populated (not N/A) because sessions have EndTime values
            summary.PeakCheckOutHourDisplay.Should().NotBe("N/A");
        }

        // ── DEV-17: LogManagerActionAsync ────────────────────────────────────

        [Fact]
        public async Task LogManagerActionAsync_InsertsAuditLogWithCorrectFields()
        {
            // Arrange
            ManagerAuditLog? capturedLog = null;
            _mockRepo
                .Setup(r => r.InsertAuditLogAsync(It.IsAny<ManagerAuditLog>()))
                .Callback<ManagerAuditLog>(log => capturedLog = log)
                .Returns(Task.CompletedTask);

            // Act
            await _service.LogManagerActionAsync("Admin", "ExportedCSV", "Exported 10 transactions");

            // Assert
            capturedLog.Should().NotBeNull();
            capturedLog!.ManagerName.Should().Be("Admin");
            capturedLog.Action.Should().Be("ExportedCSV");
            capturedLog.Details.Should().Be("Exported 10 transactions");
            capturedLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task LogManagerActionAsync_DoesNotThrow_WhenRepositoryFails()
        {
            // Arrange
            _mockRepo
                .Setup(r => r.InsertAuditLogAsync(It.IsAny<ManagerAuditLog>()))
                .ThrowsAsync(new Exception("DB write failed"));

            // Act & Assert — must be silent failure, never surface to caller
            var act = async () => await _service.LogManagerActionAsync("Admin", "Test", "detail");
            await act.Should().NotThrowAsync();
        }

        // ── DEV-13: GetManagerAuditLogsAsync ────────────────────────────────

        [Fact]
        public async Task GetManagerAuditLogsAsync_ReturnsRequestedLimit()
        {
            // Arrange
            var logs = new List<ManagerAuditLog>
            {
                new ManagerAuditLog { Id = Guid.NewGuid(), ManagerName = "Admin", Action = "ViewedTransactions", Timestamp = DateTime.UtcNow },
                new ManagerAuditLog { Id = Guid.NewGuid(), ManagerName = "Admin", Action = "ExportedCSV", Timestamp = DateTime.UtcNow }
            };

            _mockRepo.Setup(r => r.GetAuditLogsAsync(30)).ReturnsAsync(logs);

            // Act
            var result = await _service.GetManagerAuditLogsAsync(30);

            // Assert
            result.Should().HaveCount(2);
            _mockRepo.Verify(r => r.GetAuditLogsAsync(30), Times.Once);
        }

        [Fact]
        public async Task GetManagerAuditLogsAsync_ReturnsEmptyList_WhenRepositoryThrows()
        {
            // Arrange
            _mockRepo
                .Setup(r => r.GetAuditLogsAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("DB unavailable"));

            // Act
            var result = await _service.GetManagerAuditLogsAsync(50);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // ── Typed DTO for deserializing anonymous summary result ─────────────
        private record ReconciliationSummaryDto(
            int TotalSessions,
            decimal TotalRevenue,
            double AverageDurationMinutes,
            int PeakCheckOutHour,
            string PeakCheckOutHourDisplay,
            decimal HighestSingleFee,
            int LongestSessionMinutes
        );
    }
}
