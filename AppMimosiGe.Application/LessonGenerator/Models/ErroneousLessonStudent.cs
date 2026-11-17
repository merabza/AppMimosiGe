using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     შეცდომიანი გაკვეთილის მოსწავლის სტრიქონი (LessonsWithErrorsRule): შეცდომის კოდი (11 ან 14), გაკვეთილი თავისი
///     დროით, სტრიქონი და მისი მოსწავლის კონტრაქტი
/// </summary>
public sealed record ErroneousLessonStudent(
    int ErrorCode,
    int LessonId,
    DateTime LessonDt,
    int LessonStudentId,
    int StudentContractId);
