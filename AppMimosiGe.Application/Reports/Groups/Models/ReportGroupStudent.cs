using System;

namespace AppMimosiGe.Application.Reports.Groups.Models;

/// <summary>
///     ჯგუფის მოსწავლის სტრიქონი (GroupsByStudents), რომელიც თარიღისთვის მოქმედებს: ტარიფი და დაწყება
/// </summary>
public sealed record ReportGroupStudent(
    int GbsId,
    int GroupId,
    int StudentContractId,
    float FourWeekHours,
    decimal FourWeekFee,
    float HoursCoefficient,
    DateTime StartDate);
