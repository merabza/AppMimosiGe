using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     გაკვეთილის მოსწავლის სტრიქონი, რომელიც ჯგუფის მოსწავლის სტრიქონით გაკვეთილის ჯგუფს ეკუთვნის (Access-ის
///     VR16TSBase1ChargeDates-ის JOIN-ები). მასწავლებელი, შემცვლელი, სქემა, თარიღი და სტატუსი გაკვეთილისაა
/// </summary>
public sealed record SalaryLessonStudentRow(
    int LessonId,
    int GroupId,
    int TeacherContractId,
    int? SubstituteTeacherContractId,
    int SalarySchemeId,
    DateTime LessonDt,
    int LessonStatusId,
    float HoursCount);

/// <summary>
///     თანამშრომლის კონტრაქტის ხელფასის პარამეტრები
/// </summary>
public sealed record SalaryContractData(int Id, bool PensionScheme, bool IndEnt, bool NextMonth);

/// <summary>
///     უწყისის მდგენელი, რომელსაც გამოთვლა ჯამავს (ხელით შეტანილი, ან გამოთვლით შექმნილი ტიპი 1)
/// </summary>
public sealed record SalaryPartData(int TeacherContractId, int? SalaryPartTypeId, decimal Amount);

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
