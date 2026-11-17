using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Asms.Application.Abstractions;
using Asms.Application.Security;
using Asms.Application.Students;
using Asms.Contracts.Students;
using Asms.Domain.Auditing;
using Asms.Domain.Enums;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Domain.Students;
using Asms.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Asms.Infrastructure.Students;

public sealed class StudentService : IStudentService
{
    private const int MaximumNumberAttempts = 5;

    private static readonly HashSet<string> SchoolWideRoles =
    [
        SystemRoles.SchoolOwner,
        SystemRoles.SchoolPrincipal,
        SystemRoles.AcademicManager,
        SystemRoles.EducationSupervisor,
        SystemRoles.Receptionist,
        SystemRoles.Accountant,
        SystemRoles.Librarian
    ];

    private readonly AsmsDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly IUserPermissionService _permissions;
    private readonly IRecordAccessService _recordAccess;
    private readonly IStudentNumberGenerator _numberGenerator;

    public StudentService(
        AsmsDbContext db,
        ICurrentTenant tenant,
        IUserPermissionService permissions,
        IRecordAccessService recordAccess,
        IStudentNumberGenerator numberGenerator)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _recordAccess = recordAccess;
        _numberGenerator = numberGenerator;
    }

    public async Task<StudentResult<StudentDto>> CreateAsync(
        CreateStudentRequest request,
        CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(Permissions.Students.Create, ct))
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        if (_tenant.SchoolId is not Guid schoolId)
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        var validationCode = ValidateStudentFields(request);
        if (validationCode is not null)
        {
            return StudentResult<StudentDto>.Failure(validationCode);
        }

        var requestedGuardians = request.Guardians ?? Array.Empty<GuardianLinkDto>();
        validationCode = await ValidateGuardianLinksAsync(requestedGuardians, schoolId, ct);
        if (validationCode is not null)
        {
            return StudentResult<StudentDto>.Failure(validationCode);
        }

        var school = await _db.Schools
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == schoolId && !item.IsDeleted, ct);
        if (school is null || string.IsNullOrWhiteSpace(school.Code))
        {
            return StudentResult<StudentDto>.Failure("STUDENT_SCHOOL_CONFIG_INVALID");
        }

        var student = CreateStudent(request);
        student.SchoolId = schoolId;
        student.RefreshDuplicateKey();

        var duplicates = await FindDuplicatesAsync(schoolId, student.DuplicateKey, null, ct);
        if (duplicates.Count > 0 && !request.ConfirmDuplicate)
        {
            return StudentResult<StudentDto>.Failure("STUDENT_DUPLICATE_WARNING", duplicates);
        }

        for (var attempt = 0; attempt < MaximumNumberAttempts; attempt++)
        {
            student.StudentNumber = await _numberGenerator.GenerateAsync(
                schoolId,
                school.Code,
                request.AdmissionDate.Year,
                ct);

            var guardians = CreateGuardianLinks(
                student,
                requestedGuardians,
                schoolId);
            var audit = CreateAuditLog(
                schoolId,
                student.Id,
                "Student.Created",
                newValues: CreatedAuditValues(student));

            _db.Students.Add(student);
            _db.StudentGuardians.AddRange(guardians);
            _db.AuditLogs.Add(audit);

            try
            {
                await _db.SaveChangesAsync(ct);
                return StudentResult<StudentDto>.Success(
                    await ToDtoAsync(student, ct));
            }
            catch (DbUpdateException exception) when (IsNumberCollision(exception))
            {
                _db.ChangeTracker.Clear();
                if (attempt + 1 == MaximumNumberAttempts)
                {
                    return StudentResult<StudentDto>.Failure("STUDENT_NUMBER_TAKEN");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)), ct);
                student = CreateStudent(request);
                student.SchoolId = schoolId;
                student.RefreshDuplicateKey();
            }
        }

        return StudentResult<StudentDto>.Failure("STUDENT_NUMBER_TAKEN");
    }

    public async Task<StudentResult<StudentDto>> UpdateAsync(
        Guid studentId,
        UpdateStudentRequest request,
        CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(Permissions.Students.Edit, ct))
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        if (_tenant.SchoolId is not Guid schoolId)
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        if (studentId == Guid.Empty || request.Version < 1)
        {
            return StudentResult<StudentDto>.Failure("STUDENT_INVALID_INPUT");
        }

        var student = await _db.Students
            .SingleOrDefaultAsync(item => item.Id == studentId && item.SchoolId == schoolId, ct);
        if (student is null)
        {
            return StudentResult<StudentDto>.Failure("STUDENT_NOT_FOUND");
        }

        if (!await _recordAccess.CanAccessStudentAsync(studentId, ct))
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        if (student.Version != request.Version)
        {
            return StudentResult<StudentDto>.Failure("STUDENT_VERSION_CONFLICT");
        }

        var validationCode = ValidateStudentFields(request);
        if (validationCode is not null)
        {
            return StudentResult<StudentDto>.Failure(validationCode);
        }

        if (request.Guardians is not null)
        {
            validationCode = await ValidateGuardianLinksAsync(request.Guardians, schoolId, ct);
            if (validationCode is not null)
            {
                return StudentResult<StudentDto>.Failure(validationCode);
            }
        }

        var oldDuplicateKey = student.DuplicateKey;
        var newDuplicateKey = StudentDuplicateKey.Create(
            request.FirstName,
            request.FatherName,
            request.DateOfBirth);
        var identityChanged = !string.Equals(oldDuplicateKey, newDuplicateKey, StringComparison.Ordinal);
        if (identityChanged)
        {
            var duplicates = await FindDuplicatesAsync(schoolId, newDuplicateKey, student.Id, ct);
            if (duplicates.Count > 0 && !request.ConfirmDuplicate)
            {
                return StudentResult<StudentDto>.Failure("STUDENT_DUPLICATE_WARNING", duplicates);
            }
        }

        var oldValues = StudentAuditValues(student);
        ApplyStudentFields(student, request);
        student.RefreshDuplicateKey();
        var newValues = StudentAuditValues(student);
        var oldChanges = new Dictionary<string, object?>(StringComparer.Ordinal);
        var newChanges = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in oldValues.Keys)
        {
            if (!Equals(oldValues[key], newValues[key]))
            {
                oldChanges.Add(key, oldValues[key]);
                newChanges.Add(key, newValues[key]);
            }
        }

        if (request.Guardians is not null)
        {
            await UpdateGuardianLinksAsync(student, request.Guardians, schoolId, ct);
        }

        student.UpdatedAtUtc = DateTime.UtcNow;
        var audit = CreateAuditLog(schoolId, student.Id, "Student.Updated", oldChanges, newChanges);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var versionGuard = await _db.Students
            .IgnoreQueryFilters()
            .Where(item =>
                item.Id == studentId &&
                item.SchoolId == schoolId &&
                !item.IsDeleted &&
                item.Version == request.Version)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Version, item => item.Version),
                ct);
        if (versionGuard != 1)
        {
            await transaction.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return StudentResult<StudentDto>.Failure("STUDENT_VERSION_CONFLICT");
        }

        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return StudentResult<StudentDto>.Success(await ToDtoAsync(student, ct));
    }

    public async Task<StudentResult<StudentDto>> GetAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(Permissions.Students.View, ct))
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        if (_tenant.SchoolId is not Guid schoolId || studentId == Guid.Empty)
        {
            return StudentResult<StudentDto>.Failure(
                studentId == Guid.Empty ? "STUDENT_INVALID_INPUT" : "FORBIDDEN");
        }

        var access = await _permissions.GetAccessAsync(ct);
        if (!CanViewWholeSchool(access) &&
            !await _recordAccess.CanAccessStudentAsync(studentId, ct))
        {
            return StudentResult<StudentDto>.Failure("FORBIDDEN");
        }

        var student = await _db.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.Id == studentId &&
                item.SchoolId == schoolId,
                ct);
        return student is null
            ? StudentResult<StudentDto>.Failure("STUDENT_NOT_FOUND")
            : StudentResult<StudentDto>.Success(await ToDtoAsync(student, ct));
    }

    public async Task<StudentResult<PagedResult<StudentListItemDto>>> SearchAsync(
        StudentSearchQuery query,
        CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(Permissions.Students.View, ct))
        {
            return StudentResult<PagedResult<StudentListItemDto>>.Failure("FORBIDDEN");
        }

        if (_tenant.SchoolId is not Guid schoolId)
        {
            return StudentResult<PagedResult<StudentListItemDto>>.Failure("FORBIDDEN");
        }

        if (query.Page < 1 ||
            query.PageSize is < 1 or > 100 ||
            query.Text?.Length > 200 ||
            (query.Status is not null && !TryParseStatus(query.Status, out _)))
        {
            return StudentResult<PagedResult<StudentListItemDto>>.Failure("STUDENT_INVALID_INPUT");
        }

        var access = await _permissions.GetAccessAsync(ct);
        IQueryable<Student> students = _db.Students
            .AsNoTracking()
            .Where(student => student.SchoolId == schoolId);
        if (!CanViewWholeSchool(access))
        {
            var accessibleIds = await _recordAccess.GetAccessibleStudentIdsAsync(ct);
            if (accessibleIds.Count == 0)
            {
                return StudentResult<PagedResult<StudentListItemDto>>.Success(
                    new PagedResult<StudentListItemDto>([], query.Page, query.PageSize, 0));
            }

            students = students.Where(student => accessibleIds.Contains(student.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            TryParseStatus(query.Status, out var status);
            students = students.Where(student => student.Status == status);
        }

        var searchText = query.Text?.Trim();
        if (!string.IsNullOrEmpty(searchText))
        {
            var variants = SearchVariants(searchText);
            students = students.Where(BuildSearchPredicate(variants));
        }

        var candidates = await students
            .OrderBy(student => student.LastName)
            .ThenBy(student => student.FirstName)
            .ThenBy(student => student.StudentNumber)
            .Select(student => new SearchCandidate(
                student.Id,
                student.StudentNumber,
                student.FirstName,
                student.LastName,
                student.FatherName,
                student.DateOfBirth,
                student.AdmissionDate,
                student.Status,
                student.Version))
            .ToListAsync(ct);

        IEnumerable<SearchCandidate> matching = candidates;
        if (!string.IsNullOrEmpty(searchText))
        {
            var normalizedText = StudentDuplicateKey.NormalizeName(searchText);
            matching = candidates.Where(candidate =>
                candidate.StudentNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                StudentDuplicateKey.NormalizeName(candidate.FirstName).Contains(normalizedText, StringComparison.Ordinal) ||
                StudentDuplicateKey.NormalizeName(candidate.LastName).Contains(normalizedText, StringComparison.Ordinal) ||
                StudentDuplicateKey.NormalizeName(candidate.FatherName).Contains(normalizedText, StringComparison.Ordinal));
        }

        var ordered = matching
            .OrderBy(candidate => candidate.LastName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.FirstName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.StudentNumber, StringComparer.Ordinal)
            .ToArray();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(candidate => new StudentListItemDto(
                candidate.Id,
                candidate.StudentNumber,
                candidate.FirstName,
                candidate.LastName,
                candidate.FatherName,
                candidate.DateOfBirth,
                candidate.AdmissionDate,
                candidate.Status.ToString(),
                candidate.Version))
            .ToArray();

        return StudentResult<PagedResult<StudentListItemDto>>.Success(
            new PagedResult<StudentListItemDto>(
                items,
                query.Page,
                query.PageSize,
                ordered.Length));
    }

    private async Task<bool> HasPermissionAsync(string permission, CancellationToken ct)
    {
        var access = await _permissions.GetAccessAsync(ct);
        return access.Has(permission) && _tenant.SchoolId is not null;
    }

    private static bool CanViewWholeSchool(UserAccess access) =>
        access.IsValid && access.Roles.Any(SchoolWideRoles.Contains);

    private async Task<string?> ValidateGuardianLinksAsync(
        IReadOnlyList<GuardianLinkDto> links,
        Guid schoolId,
        CancellationToken ct)
    {
        if (links.Count == 0)
        {
            return "STUDENT_GUARDIAN_REQUIRED";
        }

        foreach (var link in links)
        {
            if (!Enum.TryParse<GuardianRelationship>(link.Relationship, true, out var relationship) ||
                !Enum.IsDefined(relationship))
            {
                return "STUDENT_INVALID_INPUT";
            }

            if (link.GuardianId is Guid guardianId)
            {
                if (guardianId == Guid.Empty ||
                    !string.IsNullOrWhiteSpace(link.FullName) ||
                    !string.IsNullOrWhiteSpace(link.PhoneNumber) ||
                    !await _db.Guardians.AnyAsync(guardian =>
                        guardian.Id == guardianId &&
                        guardian.SchoolId == schoolId &&
                        !guardian.IsDeleted,
                        ct))
                {
                    return "STUDENT_GUARDIAN_INVALID";
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(link.FullName) ||
                string.IsNullOrWhiteSpace(link.PhoneNumber))
            {
                return "STUDENT_GUARDIAN_REQUIRED";
            }

            if (link.FullName.Length > 200 ||
                link.PhoneNumber.Length > 30 ||
                link.Email?.Length > 254)
            {
                return "STUDENT_INVALID_INPUT";
            }
        }

        if (links.Select(link => link.GuardianId)
            .Where(id => id.HasValue)
            .Distinct()
            .Count() != links.Count(link => link.GuardianId.HasValue))
        {
            return "STUDENT_GUARDIAN_INVALID";
        }

        return null;
    }

    private static string? ValidateStudentFields(CreateStudentRequest request) =>
        ValidateStudentFields(
            request.FirstName,
            request.LastName,
            request.FatherName,
            request.GrandfatherName,
            request.DateOfBirth,
            request.Gender,
            request.Province,
            request.District,
            request.Address,
            request.PhoneNumber,
            request.PreviousSchool,
            request.AdmissionDate,
            request.Status);

    private static string? ValidateStudentFields(UpdateStudentRequest request) =>
        ValidateStudentFields(
            request.FirstName,
            request.LastName,
            request.FatherName,
            request.GrandfatherName,
            request.DateOfBirth,
            request.Gender,
            request.Province,
            request.District,
            request.Address,
            request.PhoneNumber,
            request.PreviousSchool,
            request.AdmissionDate,
            request.Status);

    private static string? ValidateStudentFields(
        string? firstName,
        string? lastName,
        string? fatherName,
        string? grandfatherName,
        DateOnly dateOfBirth,
        string? gender,
        string? province,
        string? district,
        string? address,
        string? phoneNumber,
        string? previousSchool,
        DateOnly admissionDate,
        string? status)
    {
        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(fatherName))
        {
            return "STUDENT_REQUIRED_FIELD";
        }

        if (firstName.Length > 200 ||
            lastName.Length > 200 ||
            fatherName.Length > 200 ||
            grandfatherName?.Length > 200 ||
            province?.Length > 200 ||
            district?.Length > 200 ||
            address?.Length > 500 ||
            phoneNumber?.Length > 30 ||
            previousSchool?.Length > 200)
        {
            return "STUDENT_INVALID_INPUT";
        }

        if (dateOfBirth < new DateOnly(1900, 1, 1) ||
            dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow) ||
            admissionDate < dateOfBirth)
        {
            return "STUDENT_INVALID_DATES";
        }

        if (gender is not null &&
            (!Enum.TryParse<Gender>(gender, true, out var parsedGender) ||
             !Enum.IsDefined(parsedGender)))
        {
            return "STUDENT_INVALID_INPUT";
        }

        if (status is not null && !TryParseStatus(status, out _))
        {
            return "STUDENT_INVALID_INPUT";
        }

        return null;
    }

    private static bool TryParseStatus(string status, out StudentStatus parsed) =>
        Enum.TryParse(status, true, out parsed) && Enum.IsDefined(parsed);

    private static Student CreateStudent(CreateStudentRequest request)
    {
        var student = new Student();
        ApplyStudentFields(student, request);
        return student;
    }

    private static void ApplyStudentFields(Student student, CreateStudentRequest request)
    {
        student.FirstName = request.FirstName!.Trim();
        student.LastName = request.LastName!.Trim();
        student.FatherName = request.FatherName!.Trim();
        student.GrandfatherName = TrimToNull(request.GrandfatherName);
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = ParseGender(request.Gender);
        student.Province = TrimToNull(request.Province);
        student.District = TrimToNull(request.District);
        student.Address = TrimToNull(request.Address);
        student.PhoneNumber = TrimToNull(request.PhoneNumber);
        student.PreviousSchool = TrimToNull(request.PreviousSchool);
        student.AdmissionDate = request.AdmissionDate;
        student.Status = ParseStatus(request.Status);
    }

    private static void ApplyStudentFields(Student student, UpdateStudentRequest request)
    {
        student.FirstName = request.FirstName!.Trim();
        student.LastName = request.LastName!.Trim();
        student.FatherName = request.FatherName!.Trim();
        student.GrandfatherName = TrimToNull(request.GrandfatherName);
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = ParseGender(request.Gender);
        student.Province = TrimToNull(request.Province);
        student.District = TrimToNull(request.District);
        student.Address = TrimToNull(request.Address);
        student.PhoneNumber = TrimToNull(request.PhoneNumber);
        student.PreviousSchool = TrimToNull(request.PreviousSchool);
        student.AdmissionDate = request.AdmissionDate;
        student.Status = ParseStatus(request.Status);
    }

    private IReadOnlyList<StudentGuardian> CreateGuardianLinks(
        Student student,
        IReadOnlyList<GuardianLinkDto> requestLinks,
        Guid schoolId)
    {
        var links = new List<StudentGuardian>(requestLinks.Count);
        foreach (var request in requestLinks)
        {
            var guardianId = request.GuardianId;
            if (guardianId is null)
            {
                var guardian = new Guardian
                {
                    SchoolId = schoolId,
                    FullName = request.FullName!.Trim(),
                    PhoneNumber = request.PhoneNumber!.Trim(),
                    Email = TrimToNull(request.Email)
                };
                _db.Guardians.Add(guardian);
                guardianId = guardian.Id;
            }

            links.Add(new StudentGuardian
            {
                SchoolId = schoolId,
                StudentId = student.Id,
                GuardianId = guardianId.Value,
                Relationship = Enum.Parse<GuardianRelationship>(request.Relationship, true),
                IsPrimaryContact = request.IsPrimaryContact,
                IsEmergencyContact = request.IsEmergencyContact
            });
        }

        return links;
    }

    private async Task UpdateGuardianLinksAsync(
        Student student,
        IReadOnlyList<GuardianLinkDto> requestLinks,
        Guid schoolId,
        CancellationToken ct)
    {
        var existingLinks = await _db.StudentGuardians
            .IgnoreQueryFilters()
            .Where(link => link.SchoolId == schoolId && link.StudentId == student.Id)
            .ToListAsync(ct);
        var desiredGuardianIds = new HashSet<Guid>();

        foreach (var request in requestLinks)
        {
            Guid guardianId;
            if (request.GuardianId is Guid existingGuardianId)
            {
                guardianId = existingGuardianId;
            }
            else
            {
                var guardian = new Guardian
                {
                    SchoolId = schoolId,
                    FullName = request.FullName!.Trim(),
                    PhoneNumber = request.PhoneNumber!.Trim(),
                    Email = TrimToNull(request.Email)
                };
                _db.Guardians.Add(guardian);
                guardianId = guardian.Id;
            }

            desiredGuardianIds.Add(guardianId);
            var existingLink = existingLinks.SingleOrDefault(link => link.GuardianId == guardianId);
            if (existingLink is null)
            {
                _db.StudentGuardians.Add(new StudentGuardian
                {
                    SchoolId = schoolId,
                    StudentId = student.Id,
                    GuardianId = guardianId,
                    Relationship = Enum.Parse<GuardianRelationship>(request.Relationship, true),
                    IsPrimaryContact = request.IsPrimaryContact,
                    IsEmergencyContact = request.IsEmergencyContact
                });
            }
            else
            {
                existingLink.IsDeleted = false;
                existingLink.Relationship = Enum.Parse<GuardianRelationship>(request.Relationship, true);
                existingLink.IsPrimaryContact = request.IsPrimaryContact;
                existingLink.IsEmergencyContact = request.IsEmergencyContact;
            }
        }

        foreach (var oldLink in existingLinks.Where(link => !link.IsDeleted))
        {
            if (!desiredGuardianIds.Contains(oldLink.GuardianId))
            {
                oldLink.IsDeleted = true;
            }
        }
    }

    private async Task<List<DuplicateMatchDto>> FindDuplicatesAsync(
        Guid schoolId,
        string duplicateKey,
        Guid? exceptStudentId,
        CancellationToken ct)
    {
        return await _db.Students
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(student =>
                student.SchoolId == schoolId &&
                !student.IsDeleted &&
                student.DuplicateKey == duplicateKey &&
                (exceptStudentId == null || student.Id != exceptStudentId))
            .OrderBy(student => student.StudentNumber)
            .Select(student => new DuplicateMatchDto(
                student.Id,
                student.StudentNumber,
                student.FirstName,
                student.LastName,
                student.FatherName,
                student.DateOfBirth))
            .ToListAsync(ct);
    }

    private async Task<StudentDto> ToDtoAsync(Student student, CancellationToken ct)
    {
        var guardians = await GetGuardianDtosAsync(student.Id, student.SchoolId, ct);
        return new StudentDto(
            student.Id,
            student.StudentNumber,
            student.FirstName,
            student.LastName,
            student.FatherName,
            student.GrandfatherName,
            student.DateOfBirth,
            student.Gender?.ToString(),
            student.Province,
            student.District,
            student.Address,
            student.PhoneNumber,
            student.PreviousSchool,
            student.AdmissionDate,
            student.Status.ToString(),
            student.Version,
            guardians);
    }

    private async Task<List<GuardianLinkDto>> GetGuardianDtosAsync(
        Guid studentId,
        Guid schoolId,
        CancellationToken ct)
    {
        return await (
            from link in _db.StudentGuardians.AsNoTracking()
            join guardian in _db.Guardians.AsNoTracking()
                on new { link.GuardianId, link.SchoolId }
                equals new { GuardianId = guardian.Id, guardian.SchoolId }
            where link.StudentId == studentId &&
                link.SchoolId == schoolId &&
                guardian.SchoolId == schoolId
            orderby link.CreatedAtUtc, link.Id
            select new GuardianLinkDto(
                guardian.Id,
                guardian.FullName,
                guardian.PhoneNumber,
                guardian.Email,
                link.Relationship.ToString(),
                link.IsPrimaryContact,
                link.IsEmergencyContact))
            .ToListAsync(ct);
    }

    private AuditLog CreateAuditLog(
        Guid schoolId,
        Guid studentId,
        string action,
        Dictionary<string, object?>? oldValues = null,
        Dictionary<string, object?>? newValues = null)
    {
        return new AuditLog
        {
            SchoolId = schoolId,
            UserId = _tenant.UserId,
            Action = action,
            EntityType = "Student",
            EntityId = studentId,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    private static Dictionary<string, object?> CreatedAuditValues(Student student) =>
        new(StringComparer.Ordinal)
        {
            ["id"] = student.Id,
            ["studentNumber"] = student.StudentNumber,
            ["firstName"] = student.FirstName,
            ["lastName"] = student.LastName,
            ["fatherName"] = student.FatherName,
            ["grandfatherName"] = student.GrandfatherName
        };

    private static Dictionary<string, object?> StudentAuditValues(Student student) =>
        new(StringComparer.Ordinal)
        {
            ["firstName"] = student.FirstName,
            ["lastName"] = student.LastName,
            ["fatherName"] = student.FatherName,
            ["grandfatherName"] = student.GrandfatherName,
            ["dateOfBirth"] = student.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["gender"] = student.Gender?.ToString(),
            ["previousSchool"] = student.PreviousSchool,
            ["admissionDate"] = student.AdmissionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["status"] = student.Status.ToString()
        };

    private static bool IsNumberCollision(DbUpdateException exception)
    {
        var cause = exception.GetBaseException();
        if (cause is PostgresException postgres)
        {
            return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                string.Equals(
                    postgres.ConstraintName,
                    "UX_Students_SchoolId_StudentNumber",
                    StringComparison.Ordinal);
        }

        if (cause is SqliteException sqlite)
        {
            return (sqlite.SqliteExtendedErrorCode is 1555 or 2067) &&
                sqlite.Message.Contains(
                    "Students.SchoolId, Students.StudentNumber",
                    StringComparison.Ordinal);
        }

        return false;
    }

    private static Expression<Func<Student, bool>> BuildSearchPredicate(
        IReadOnlyList<string> variants)
    {
        var student = Expression.Parameter(typeof(Student), "student");
        Expression body = Expression.Constant(false);
        foreach (var propertyName in new[]
                 {
                     nameof(Student.StudentNumber),
                     nameof(Student.FirstName),
                     nameof(Student.LastName),
                     nameof(Student.FatherName)
                 })
        {
            var property = Expression.Property(student, propertyName);
            foreach (var variant in variants)
            {
                var contains = Expression.Call(
                    property,
                    nameof(string.Contains),
                    Type.EmptyTypes,
                    Expression.Constant(variant));
                body = Expression.OrElse(body, contains);
            }
        }

        return Expression.Lambda<Func<Student, bool>>(body, student);
    }

    private static IReadOnlyList<string> SearchVariants(string searchText)
    {
        var normalized = StudentDuplicateKey.NormalizeName(searchText);
        var variants = new HashSet<string>(StringComparer.Ordinal)
        {
            searchText.Trim(),
            normalized
        };
        foreach (var character in normalized)
        {
            if (character is '\u06CC' or '\u06A9')
            {
                variants.Add(normalized.Replace(character, character == '\u06CC' ? '\u064A' : '\u0643'));
            }
        }

        return variants.Where(value => value.Length > 0).ToArray();
    }

    private static Gender? ParseGender(string? gender) =>
        string.IsNullOrWhiteSpace(gender) ? null : Enum.Parse<Gender>(gender, true);

    private static StudentStatus ParseStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
            ? StudentStatus.Active
            : Enum.Parse<StudentStatus>(status, true);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record SearchCandidate(
        Guid Id,
        string StudentNumber,
        string FirstName,
        string LastName,
        string FatherName,
        DateOnly DateOfBirth,
        DateOnly AdmissionDate,
        StudentStatus Status,
        long Version);
}
