using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Homework;

/// <summary>
/// Submissions are optional because some families have no internet; teachers can record feedback without one.
/// </summary>
public class HomeworkSubmission : BaseEntity
{
    public Guid HomeworkAssignmentId { get; set; }

    public Guid StudentId { get; set; }

    public HomeworkSubmissionStatus Status { get; set; } = HomeworkSubmissionStatus.NotSubmitted;

    public DateTime? SubmittedAtUtc { get; set; }

    public string? SubmissionText { get; set; }

    public string? AttachmentPath { get; set; }

    public string? TeacherFeedback { get; set; }

    public decimal? Score { get; set; }
}
