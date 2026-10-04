using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     გამოთვლის საწყისი მონაცემები. LessonRows დარიცხვის თარიღის წინა და იმავე თვის გაკვეთილებია; OperationMonths
///     სამუშაო თვეების პირველი რიცხვები; HourRates სქემის საათობრივი ხელფასი ხელზე; PartTypeCountPlaces ტიპის
///     გამოთვლის ადგილი; ManualParts უწყისის ტიპი 1-ის გარდა ყველა მდგენელი
/// </summary>
public sealed record SalaryCalculationInput(
    DateTime ChargeDate,
    IReadOnlyList<SalaryContractData> Contracts,
    IReadOnlyList<SalaryLessonStudentRow> LessonRows,
    IReadOnlySet<DateTime> OperationMonths,
    IReadOnlyDictionary<int, decimal> HourRates,
    IReadOnlyDictionary<int, int?> PartTypeCountPlaces,
    IReadOnlyList<SalaryPartData> ManualParts);
