using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     ჩატარებული გაკვეთილების ხელფასი (მდგენელი ტიპი 1)
/// </summary>
public sealed record CalculatedSalaryPart(int TeacherContractId, decimal Amount);

/// <summary>
///     სტრიქონის დეტალი: ჯგუფის თანხა, საათები და ერთი საათის ღირებულება
/// </summary>
public sealed record CalculatedSalaryLineDetail(int GroupId, decimal Amount, float HoursCount, decimal HourCost);

/// <summary>
///     უწყისის სტრიქონი
/// </summary>
public sealed record CalculatedSalaryLine(
    int TeacherContractId,
    decimal NetAmountRound,
    decimal AmountGross,
    decimal Pension2,
    decimal GrossMinusPension,
    decimal IncomeTax,
    decimal Gamokvitva,
    decimal Pension4,
    decimal AmountNet,
    DateTime MonthDate,
    int RsQuoteTypeId,
    decimal IndividualIncomeTax,
    IReadOnlyList<CalculatedSalaryLineDetail> Details);

public sealed record SalaryCalculationResult(
    IReadOnlyList<CalculatedSalaryPart> LessonParts,
    IReadOnlyList<CalculatedSalaryLine> Lines);
