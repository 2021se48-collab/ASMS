using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Homework;

namespace Asms.Domain.Tests.Homework;

public class HomeworkTests
{
    [Fact]
    public void Homework_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new HomeworkAssignment());
        Assert.IsAssignableFrom<ITenantEntity>(new HomeworkSubmission());
    }

    [Fact]
    public void Homework_entities_have_expected_defaults()
    {
        var assignment = new HomeworkAssignment();
        var submission = new HomeworkSubmission();

        Assert.Equal(string.Empty, assignment.Title);
        Assert.False(assignment.AllowSubmissions);
        Assert.Equal(HomeworkSubmissionStatus.NotSubmitted, submission.Status);
        Assert.Null(submission.Score);
        Assert.Null(submission.SubmittedAtUtc);
    }

    [Fact]
    public void IsOverdue_is_false_on_due_date_and_before_due_date()
    {
        var dueDate = new DateOnly(2026, 11, 13);
        var assignment = new HomeworkAssignment { DueDate = dueDate };

        Assert.False(assignment.IsOverdue(dueDate));
        Assert.False(assignment.IsOverdue(dueDate.AddDays(-1)));
    }

    [Fact]
    public void IsOverdue_is_true_after_due_date()
    {
        var dueDate = new DateOnly(2026, 11, 13);
        var assignment = new HomeworkAssignment { DueDate = dueDate };

        Assert.True(assignment.IsOverdue(dueDate.AddDays(1)));
    }
}
