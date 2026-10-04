using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     r17-ის საწყისი მონაცემები: გაცდენები (თარიღისთვის აქტიური ჯგუფის მოქმედი სტრიქონიდან, მხოლოდ დაწყებული
///     გაკვეთილები), ამ კონტრაქტების ბოლო დასწრება (იმავე ზღვრამდე) და მოსწავლეების საკონტაქტო მონაცემები
/// </summary>
public sealed record MissingsInRowData(
    IReadOnlyList<StudentAbsence> Absences,
    IReadOnlyDictionary<int, DateTime> LastPresences,
    IReadOnlyDictionary<int, StudentContact> Students);
