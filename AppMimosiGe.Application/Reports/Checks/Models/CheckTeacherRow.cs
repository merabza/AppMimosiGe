using System;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     ჯგუფის მასწავლებლის სტრიქონი (GroupsByTeachers) ხელფასის სქემით
/// </summary>
public sealed record CheckTeacherRow(
    int GbtId,
    int GroupId,
    int TeacherContractId,
    int SalarySchemaId,
    DateTime StartDate,
    DateTime? EndDate);
