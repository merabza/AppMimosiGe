using System;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     ჯგუფის მოსწავლის სტრიქონი (GroupsByStudents) ტარიფით
/// </summary>
public sealed record CheckStudentRow(
    int GbsId,
    int GroupId,
    int StudentContractId,
    DateTime StartDate,
    DateTime? EndDate,
    float FourWeekHours,
    decimal FourWeekFee,
    decimal OneHourFee,
    float HoursCoefficient);
