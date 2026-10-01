using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     ჯგუფის მოსწავლე (GroupsByStudents) პერიოდით [StartDate, EndDate)
/// </summary>
public sealed record GeneratorStudentRow(
    int GbsId,
    int StudentContractId,
    float HoursCoefficient,
    float FourWeekHours,
    DateTime StartDate,
    DateTime? EndDate);
