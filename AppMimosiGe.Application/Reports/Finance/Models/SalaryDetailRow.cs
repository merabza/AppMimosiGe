using System;

namespace AppMimosiGe.Application.Reports.Finance.Models;

/// <summary>
///     ხელფასის დეტალი (SalaryLinesDetails) სტრიქონის თვით და მასწავლებლით. TeacherName Access-ის
///     Nz([LegalName], [FirstName])-ია: იურიდიული სახელი, თუ აქვს
/// </summary>
public sealed record SalaryDetailRow(
    int SadId,
    DateTime MonthDate,
    int TeacherContractId,
    string TeacherLastName,
    string TeacherName,
    string ContractNumber,
    int GroupId,
    string GroupCode,
    string CourseName,
    float HoursCount,
    decimal HourCost,
    decimal Amount);
