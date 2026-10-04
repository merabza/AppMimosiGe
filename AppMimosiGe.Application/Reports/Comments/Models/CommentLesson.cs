using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Comments.Models;

/// <summary>
///     r01-ის გაკვეთილი: ჯგუფი, საგანი, გაკვეთილის მასწავლებელი და შემცვლელი, აღდგენის თარიღი და მოსწავლეები
/// </summary>
public sealed record CommentLesson(
    int LessonId,
    DateTime LessonDt,
    string GroupCode,
    string CourseName,
    SchedulePerson Teacher,
    SchedulePerson? Substitute,
    DateTime? RecoverDate,
    IReadOnlyList<CommentStudent> Students);
