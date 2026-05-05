using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WorldplayAMS.API.Services;
using WorldplayAMS.Core.Models;
using Xunit;

namespace WorldplayAMS.Tests.Services
{
    public class EmailServiceTests
    {
        private readonly Mock<ILogger<EmailService>> _mockLogger;

        public EmailServiceTests()
        {
            _mockLogger = new Mock<ILogger<EmailService>>();
        }

        private EmailService CreateService(string? host = null, string? port = null,
            string? username = null, string? password = null, string? fromEmail = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    { "Smtp:Host",      host ?? string.Empty },
                    { "Smtp:Port",      port ?? string.Empty },
                    { "Smtp:Username",  username ?? string.Empty },
                    { "Smtp:Password",  password ?? string.Empty },
                    { "Smtp:FromEmail", fromEmail ?? "no-reply@worldplay.com" }
                })
                .Build();

            return new EmailService(config, _mockLogger.Object);
        }

        private static DigitalReceipt SampleReceipt() => new DigitalReceipt
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ReceiptNumber = "WP-20260501-ABCD",
            GuestName = "Test Guest",
            MachineName = "Cyber Racer",
            CheckInTime = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            CheckOutTime = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 60,
            Fee = 9.00m,
            StaffName = "Staff01",
            IssuedAt = DateTime.UtcNow,
            Status = "Issued"
        };

        // ── DEV-12: Simulation Mode (no SMTP configured) ─────────────────────

        [Fact]
        public async Task SendReceiptEmailAsync_NoHost_ReturnsTrue_InSimulationMode()
        {
            // Arrange — host is empty, simulation fallback must trigger
            var service = CreateService(host: "");

            // Act
            var result = await service.SendReceiptEmailAsync("guest@example.com", SampleReceipt());

            // Assert — simulation returns success without making a real network call
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendReceiptEmailAsync_PlaceholderHost_ReturnsTrue_InSimulationMode()
        {
            // Arrange
            var service = CreateService(host: "placeholder_host", port: "587", username: "user", password: "pass");

            // Act
            var result = await service.SendReceiptEmailAsync("guest@example.com", SampleReceipt());

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendReceiptEmailAsync_PlaceholderPassword_ReturnsTrue_InSimulationMode()
        {
            // Arrange — real host but password is still a PASTE_ placeholder
            var service = CreateService(
                host: "sandbox.smtp.mailtrap.io",
                port: "587",
                username: "someuser",
                password: "PASTE_YOUR_MAILTRAP_PASSWORD_HERE");

            // Act
            var result = await service.SendReceiptEmailAsync("guest@example.com", SampleReceipt());

            // Assert — must not attempt a real SMTP connection with a placeholder password
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendReceiptEmailAsync_EmptyPassword_ReturnsTrue_InSimulationMode()
        {
            // Arrange
            var service = CreateService(
                host: "sandbox.smtp.mailtrap.io",
                port: "587",
                username: "someuser",
                password: "");

            // Act
            var result = await service.SendReceiptEmailAsync("guest@example.com", SampleReceipt());

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendReceiptEmailAsync_InvalidPort_ReturnsTrue_InSimulationMode()
        {
            // Arrange — port is non-numeric, simulation fallback must trigger
            var service = CreateService(host: "smtp.example.com", port: "NOT_A_PORT");

            // Act
            var result = await service.SendReceiptEmailAsync("guest@example.com", SampleReceipt());

            result.Should().BeTrue();
        }

        // ── DEV-12: Real SMTP Path — Resilience ──────────────────────────────

        [Fact]
        public async Task SendReceiptEmailAsync_SmtpConnectionFails_ReturnsFalse_NotThrow()
        {
            // Arrange — valid config pointing at a host that will refuse the connection
            var service = CreateService(
                host: "127.0.0.1",   // localhost — nothing listening, connection refused
                port: "9999",
                username: "user",
                password: "password",
                fromEmail: "receipts@worldplay.com");

            // Act
            var result = await service.SendReceiptEmailAsync("guest@example.com", SampleReceipt());

            // Assert — must return false, not throw (resilience requirement)
            result.Should().BeFalse();
        }

        // ── DEV-12: Email Body Content ────────────────────────────────────────

        [Fact]
        public async Task SendReceiptEmailAsync_SimulationMode_LogsReceiptDetails()
        {
            // Arrange
            var service = CreateService(); // no SMTP — simulation mode
            var receipt = SampleReceipt();

            // Act — just verify it doesn't throw and returns true
            var result = await service.SendReceiptEmailAsync("guest@example.com", receipt);

            result.Should().BeTrue();

            // Verify logger was called (simulation logs subject + body)
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Simulating email send")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
