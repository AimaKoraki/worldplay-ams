using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Core.Interfaces;

public interface IEmailService
{
    Task<bool> SendReceiptEmailAsync(string toEmail, DigitalReceipt receipt);
}
