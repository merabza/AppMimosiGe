using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     მასწავლებლის კონტრაქტი და მისი ძირითადი სქემა საათობრივი ანაზღაურებისთვის (SalarySchemaByHours)
/// </summary>
public sealed record CheckTeacherContract(SchedulePerson Person, int? SalarySchemaByHoursId);
