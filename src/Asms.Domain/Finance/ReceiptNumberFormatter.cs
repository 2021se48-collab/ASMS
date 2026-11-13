using System.Globalization;

namespace Asms.Domain.Finance;

/// <summary>
/// Formats receipt numbers; the device code makes numbers unique across offline computers.
/// </summary>
public static class ReceiptNumberFormatter
{
    /// <summary>
    /// Formats a receipt number from a device code, date, and sequence.
    /// </summary>
    public static string Format(string deviceCode, DateOnly date, int sequence)
    {
        if (string.IsNullOrWhiteSpace(deviceCode) || deviceCode.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new ArgumentException("Device code must contain only letters and digits.", nameof(deviceCode));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}-{1:yyyyMMdd}-{2:D4}",
            deviceCode.ToUpperInvariant(),
            date,
            sequence);
    }
}
