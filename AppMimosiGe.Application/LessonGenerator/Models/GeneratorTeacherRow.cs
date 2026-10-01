using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     ჯგუფის მასწავლებელი (GroupsByTeachers) პერიოდით [StartDate, EndDate)
/// </summary>
public sealed record GeneratorTeacherRow(
    int TeacherContractId,
    int SalarySchemaId,
    DateTime StartDate,
    DateTime? EndDate);
