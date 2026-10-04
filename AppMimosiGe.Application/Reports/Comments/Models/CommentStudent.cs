using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Comments.Models;

/// <summary>
///     გაკვეთილის მოსწავლე (LessonsByStudents) და ჟურნალში შეტანილი კომენტარები
/// </summary>
public sealed record CommentStudent(
    int StudentContractId,
    SchedulePerson Student,
    string? TeacherComment,
    string? StudentComment);
