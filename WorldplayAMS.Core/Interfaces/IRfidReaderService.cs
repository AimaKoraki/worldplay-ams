using System.Threading.Tasks;
using WorldplayAMS.Core.Models;

namespace WorldplayAMS.Core.Interfaces;

public interface IRfidReaderService
{
    /// <summary>
    /// Validates an RFID tag by its UID.
    /// </summary>
    Task<RfidTag?> ValidateTagAsync(string tagUid);
}
