using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Students;

namespace Asms.Domain.Tests.Students;

public class StudentTests
{
    [Fact]
    public void New_student_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Student());
    }

    [Fact]
    public void New_student_has_active_status_and_no_gender()
    {
        var student = new Student();

        Assert.Equal(StudentStatus.Active, student.Status);
        Assert.Null(student.Gender);
    }

    [Fact]
    public void New_student_has_empty_required_strings()
    {
        var student = new Student();

        Assert.Equal(string.Empty, student.StudentNumber);
        Assert.Equal(string.Empty, student.FirstName);
        Assert.Equal(string.Empty, student.LastName);
        Assert.Equal(string.Empty, student.FatherName);
        Assert.Equal(string.Empty, student.DuplicateKey);
    }

    [Fact]
    public void RefreshDuplicateKey_sets_the_duplicate_key()
    {
        var student = new Student
        {
            FirstName = "Ahmad",
            FatherName = "Ali",
            DateOfBirth = new DateOnly(2012, 4, 5)
        };

        student.RefreshDuplicateKey();

        Assert.Equal(
            StudentDuplicateKey.Create(student.FirstName, student.FatherName, student.DateOfBirth),
            student.DuplicateKey);
    }

    [Fact]
    public void Students_with_equivalent_names_and_date_have_equal_duplicate_keys()
    {
        var dateOfBirth = new DateOnly(2012, 4, 5);
        var firstStudent = new Student
        {
            FirstName = " Ahmad ",
            FatherName = "Mohammad Ali",
            DateOfBirth = dateOfBirth
        };
        var secondStudent = new Student
        {
            FirstName = "ahmad",
            FatherName = "MOHAMMAD   ALI",
            DateOfBirth = dateOfBirth
        };

        firstStudent.RefreshDuplicateKey();
        secondStudent.RefreshDuplicateKey();

        Assert.Equal(firstStudent.DuplicateKey, secondStudent.DuplicateKey);
    }
}
