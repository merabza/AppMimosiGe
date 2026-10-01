using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     დარიცხვის საწყისი მონაცემები (Access-ის vFrmChargesAndPayments-ის პირველი ნაწილი): LessonsByStudents-ის სტრიქონი,
///     გაკვეთილის თარიღი, საგნის სახელი და მოსწავლის ჯგუფის სტრიქონის (GroupsByStudents) ტარიფი
/// </summary>
public sealed record ChargeData(
    int LessonByStudentId,
    int StudentContractId,
    DateTime LessonDt,
    string CourseName,
    decimal FourWeekFee,
    float FourWeekHours,
    float HoursCount);
