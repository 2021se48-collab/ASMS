using System.Globalization;
using System.Text;

namespace Asms.Domain.Students;

/// <summary>
/// Normalizes names because Pashto and Dari letters may have different Unicode forms.
/// </summary>
public static class StudentDuplicateKey
{
    /// <summary>
    /// Normalizes a name for comparing possible duplicate student records.
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalizedName = name.Normalize(NormalizationForm.FormKC);
        var result = new StringBuilder(normalizedName.Length);
        var pendingSpace = false;

        foreach (var character in normalizedName)
        {
            if (character == '\u0640'
                || character is >= '\u064B' and <= '\u065F'
                || character == '\u0670'
                || character is '\u200B' or '\u200C' or '\u200D' or '\uFEFF')
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (result.Length > 0)
                {
                    pendingSpace = true;
                }

                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character switch
            {
                '\u064A' or '\u0649' => '\u06CC',
                '\u0643' => '\u06A9',
                _ => character
            });
        }

        return result.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a comparison key for a student's names and date of birth.
    /// </summary>
    public static string Create(string? firstName, string? fatherName, DateOnly dateOfBirth)
    {
        return $"{NormalizeName(firstName)}|{NormalizeName(fatherName)}|{dateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }
}
