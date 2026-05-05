using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorldplayAMS.Core.Interfaces;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendReceiptEmailAsync(string toEmail, DigitalReceipt receipt)
    {
        try
        {
            var host = _configuration["Smtp:Host"];
            var portString = _configuration["Smtp:Port"];
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var fromEmail = _configuration["Smtp:FromEmail"] ?? "no-reply@worldplay.com";

            var subject = $"Your Receipt from Worldplay - {receipt.ReceiptNumber}";
            var body = GenerateEmailBody(receipt);

            // Simulation fallback if SMTP is not configured or password is still a placeholder
            if (string.IsNullOrEmpty(host) || host == "placeholder_host" || !int.TryParse(portString, out var port)
                || string.IsNullOrEmpty(password) || (password?.StartsWith("PASTE_") ?? false))
            {
                _logger.LogInformation("SMTP not configured. Simulating email send to {ToEmail}.", toEmail);
                _logger.LogInformation("Email Subject: {Subject}", subject);
                _logger.LogInformation("Email Body:\n{Body}", body);
                return true;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Worldplay AMS"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false // Using plain text for simplicity and aesthetic fit
            };
            
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Receipt email sent successfully to {ToEmail}", toEmail);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send receipt email to {ToEmail}", toEmail);
            return false;
        }
    }

    private string GenerateEmailBody(DigitalReceipt r)
    {
        var machine = r.MachineName ?? "N/A";
        var sid = r.SessionId.ToString()[..8].ToUpper();
        var lines = new List<string>
        {
            "WORLDPLAY ARCADE MANAGEMENT SYSTEM",
            "========================================",
            "DIGITAL RECEIPT",
            "========================================",
            $"Receipt #:    {r.ReceiptNumber}",
            $"Status:       {r.Status}",
            $"Issued:       {r.IssuedAt.ToLocalTime():MMM dd yyyy, HH:mm:ss}",
            "----------------------------------------",
            $"Guest Name:   {r.GuestName}",
            $"Machine:      {machine}",
            $"Session:      {sid}",
            "----------------------------------------",
            $"Check-In:     {r.CheckInTime.ToLocalTime():MMM dd yyyy, HH:mm}",
            $"Check-Out:    {r.CheckOutTime.ToLocalTime():MMM dd yyyy, HH:mm}",
            $"Duration:     {r.DurationMinutes} minutes",
            "----------------------------------------",
            $"Processed By: {r.StaffName}",
            "",
            "========================================",
            $"TOTAL:        LKR {r.Fee:F2}",
            "========================================",
            "Thank you for playing at Worldplay!"
        };

        return string.Join("\n", lines);
    }
}
