namespace Asms.Domain.Security;

public static class SystemRoles
{
    public const string PlatformSuperAdministrator = "PlatformSuperAdministrator";
    public const string SchoolOwner = "SchoolOwner";
    public const string SchoolPrincipal = "SchoolPrincipal";
    public const string AcademicManager = "AcademicManager";
    public const string Accountant = "Accountant";
    public const string Teacher = "Teacher";
    public const string Receptionist = "Receptionist";
    public const string Librarian = "Librarian";
    public const string Parent = "Parent";
    public const string Student = "Student";
    public const string EducationSupervisor = "EducationSupervisor";

    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[]
    {
        PlatformSuperAdministrator,
        SchoolOwner,
        SchoolPrincipal,
        AcademicManager,
        Accountant,
        Teacher,
        Receptionist,
        Librarian,
        Parent,
        Student,
        EducationSupervisor
    });
}
