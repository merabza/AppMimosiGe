using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     სამუშაო თვეების კალენდარი (OperationMonths) და გენერატორის ჰორიზონტი (Q6, D66)
/// </summary>
public static class OperationMonthsCalendar
{
    /// <summary>
    ///     Access-ის CheckOperationMonths: თვეები ბოლო არსებული თვის შემდეგ ემატება იმ თვემდე ჩათვლით, რომელშიც დღეს + 2 თვე
    ///     მოდის. Access-ის GetMaxDates სტრიქონების EndDate-ებსაც ადარებდა, მაგრამ `&lt;&gt; Null` შედარება VBA-ში
    ///     ყოველთვის false-ია, ამიტომ ფაქტობრივად მხოლოდ დღეს + 2 თვე ითვლებოდა
    /// </summary>
    public static IReadOnlyList<DateTime> MonthsToAdd(DateTime? lastMonth, DateTime now)
    {
        DateTime targetMonth = MonthStart(now.AddMonths(2));
        DateTime month = lastMonth is null ? targetMonth.AddMonths(-1) : MonthStart(lastMonth.Value);
        List<DateTime> months = [];
        while (month < targetMonth)
        {
            month = month.AddMonths(1);
            months.Add(month);
        }

        return months;
    }

    /// <summary>
    ///     ჰორიზონტი: ბოლო სამუშაო თვის ბოლო დღე. გაკვეთილები ამ დღის ჩათვლით იქმნება
    /// </summary>
    public static DateTime HorizonEnd(DateTime lastMonth)
    {
        return MonthStart(lastMonth).AddMonths(1).AddDays(-1);
    }

    private static DateTime MonthStart(DateTime date)
    {
        return date.Date.AddDays(1 - date.Day);
    }
}
