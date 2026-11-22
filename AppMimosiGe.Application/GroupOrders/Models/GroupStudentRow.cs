using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის მოსწავლის სტრიქონი (GroupsByStudents) ტარიფით, შენიშვნით და პერიოდით [StartDate, EndDate). LineId: ბრძანების
///     ხაზი, რომელმაც სტრიქონი შექმნა (ბაზაში GroupOrderStudentId)
/// </summary>
public sealed record GroupStudentRow(
    int LineId,
    int StudentContractId,
    float FourWeekHours,
    decimal FourWeekFee,
    decimal OneHourFee,
    float HoursCoefficient,
    string? Note,
    DateTime StartDate,
    DateTime? EndDate);
