using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     მოსწავლის კონტრაქტის გაცდენების რაოდენობა ერთ საგანში (r14)
/// </summary>
public sealed record AbsenceCountRow(int StudentContractId, SchedulePerson Student, string CourseName, int Count);
