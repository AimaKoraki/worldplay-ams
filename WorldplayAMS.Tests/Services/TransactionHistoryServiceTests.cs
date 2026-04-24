using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Tests.Services;

public class TransactionHistoryServiceTests
{
    private readonly Mock<ISupabaseRepository> _repoMock;
    private readonly Mock<ILogger<TransactionHistoryService>> _loggerMock;
    private readonly TransactionHistoryService _service;

    public TransactionHistoryServiceTests()
    {
        _repoMock = new Mock<ISupabaseRepository>();
        _loggerMock = new Mock<ILogger<TransactionHistoryService>>();
        _service = new TransactionHistoryService(_repoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetTransactionsByDateRange_ReturnsSessionsInRange()
    {
        // Arrange
        var from = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 20, 23, 59, 59, DateTimeKind.Utc);
        var expected = new List<Session>
        {
            new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = from.AddHours(1),
                EndTime = from.AddHours(3),
                Status = "Completed",
                TotalDurationMinutes = 120,
                Fee = 18.00m,
                CheckedOutByStaff = "TestStaff"
            }
        };

        _repoMock.Setup(r => r.GetSessionsByDateRangeAsync(from, to))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetTransactionsByDateRangeAsync(from, to);

        // Assert
        result.Should().HaveCount(1);
        result[0].Fee.Should().Be(18.00m);
        result[0].CheckedOutByStaff.Should().Be("TestStaff");
    }

    [Fact]
    public async Task GetTransactionsByDateRange_ReturnsEmptyOnException()
    {
        // Arrange
        var from = DateTime.UtcNow.Date;
        var to = DateTime.UtcNow.Date;
        _repoMock.Setup(r => r.GetSessionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("DB unavailable"));

        // Act
        var result = await _service.GetTransactionsByDateRangeAsync(from, to);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDailyReconciliationSummary_ComputesCorrectValues()
    {
        // Arrange
        var testDate = new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
        var sessions = new List<Session>
        {
            new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = testDate.AddHours(9),
                EndTime = testDate.AddHours(10),
                Status = "Completed",
                TotalDurationMinutes = 60,
                Fee = 9.00m,
                CheckedOutByStaff = "StaffA"
            },
            new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = testDate.AddHours(14),
                EndTime = testDate.AddHours(15).AddMinutes(30),
                Status = "Completed",
                TotalDurationMinutes = 90,
                Fee = 13.50m,
                CheckedOutByStaff = "StaffB"
            },
            new Session
            {
                Id = Guid.NewGuid(),
                RfidTagId = Guid.NewGuid(),
                StartTime = testDate.AddHours(14).AddMinutes(15),
                EndTime = testDate.AddHours(15),
                Status = "Completed",
                TotalDurationMinutes = 45,
                Fee = 6.75m,
                CheckedOutByStaff = "StaffA"
            }
        };

        _repoMock.Setup(r => r.GetSessionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(sessions);

        // Act
        var summary = await _service.GetDailyReconciliationSummaryAsync(testDate);

        // Assert
        summary.TotalSessions.Should().Be(3);
        summary.TotalRevenue.Should().Be(29.25m);
        summary.AverageDurationMinutes.Should().Be(65.0); // (60+90+45) / 3
        summary.PeakCheckOutHour.Should().Be(15); // two sessions end at hour 15
        summary.HighestSingleFee.Should().Be(13.50m);
        summary.LongestSessionMinutes.Should().Be(90);
    }

    [Fact]
    public async Task GetDailyReconciliationSummary_ReturnsDefaultOnNoSessions()
    {
        // Arrange
        _repoMock.Setup(r => r.GetSessionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Session>());

        // Act
        var summary = await _service.GetDailyReconciliationSummaryAsync(DateTime.UtcNow);

        // Assert
        summary.TotalSessions.Should().Be(0);
        summary.TotalRevenue.Should().Be(0);
        summary.AverageDurationMinutes.Should().Be(0);
    }

    [Fact]
    public async Task LogManagerAction_InsertsAuditLogEntry()
    {
        // Arrange
        _repoMock.Setup(r => r.InsertAuditLogAsync(It.IsAny<ManagerAuditLog>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.LogManagerActionAsync("TestManager", "ExportedCSV", "Exported 5 records");

        // Assert
        _repoMock.Verify(r => r.InsertAuditLogAsync(It.Is<ManagerAuditLog>(log =>
            log.ManagerName == "TestManager" &&
            log.Action == "ExportedCSV" &&
            log.Details == "Exported 5 records"
        )), Times.Once);
    }

    [Fact]
    public async Task LogManagerAction_DoesNotThrowOnFailure()
    {
        // Arrange
        _repoMock.Setup(r => r.InsertAuditLogAsync(It.IsAny<ManagerAuditLog>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var act = async () => await _service.LogManagerActionAsync("TestManager", "ViewedTransactions");

        // Assert — should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetManagerAuditLogs_ReturnsLogs()
    {
        // Arrange
        var logs = new List<ManagerAuditLog>
        {
            new ManagerAuditLog { Id = Guid.NewGuid(), ManagerName = "Admin", Action = "ViewedTransactions", Timestamp = DateTime.UtcNow }
        };
        _repoMock.Setup(r => r.GetAuditLogsAsync(50)).ReturnsAsync(logs);

        // Act
        var result = await _service.GetManagerAuditLogsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Action.Should().Be("ViewedTransactions");
    }
}
