using System.Globalization;
using Asms.Application.Students;
using Asms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Students;

public sealed class StudentNumberGenerator : IStudentNumberGenerator
{
    private readonly AsmsDbContext _db;

    public StudentNumberGenerator(AsmsDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateAsync(
        Guid schoolId,
        string schoolCode,
        int admissionYear,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolCode);

        var prefix = $"{schoolCode}-{admissionYear}-";
        var numbers = await _db.Students
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(student =>
                student.SchoolId == schoolId &&
                student.StudentNumber.StartsWith(prefix))
            .Select(student => student.StudentNumber)
            .ToListAsync(ct);

        var maximumSequence = numbers
            .Select(number => ParseSequence(number, prefix))
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{checked(maximumSequence + 1).ToString("D4", CultureInfo.InvariantCulture)}";
    }

    private static int ParseSequence(string studentNumber, string prefix)
    {
        var suffix = studentNumber.AsSpan(prefix.Length);
        return int.TryParse(
            suffix,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var sequence)
                ? sequence
                : 0;
    }
}
