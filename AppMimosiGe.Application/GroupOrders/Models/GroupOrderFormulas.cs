using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     საათებისა და ფასის ფორმულები (D187): 4 კვირის საათები = 4 × კოეფიციენტი × კვირის საათები, ხოლო 4 კვირის
///     გადასახადი საათის ფასისა და 4 კვირის საათების ნამრავლისგან 0.01-ზე მეტად არ განსხვავდება (r30-ის ზღვარი).
///     საათები float-ია, ამიტომ ზღვრით ედრება (GeneratorHours, D153)
/// </summary>
public static class GroupOrderFormulas
{
    /// <summary>
    ///     4 კვირის გადასახადის დასაშვები სხვაობა, როგორც r30-ში (MismatchReports, Access-ის Abs(…) &gt; 0.01)
    /// </summary>
    public const decimal FeeTolerance = 0.01m;

    private const int WeeksInFourWeeks = 4;

    /// <summary>
    ///     კვირის საათები: day დღეს მოქმედი განრიგის სტრიქონების HoursCount-ების ჯამი
    /// </summary>
    public static float WeeklyHours(IEnumerable<GroupDayTimePlaceRow> dayTimePlaces, DateTime day)
    {
        return dayTimePlaces.Where(d => GroupActivity.IsRowActiveOn(d.StartDate, d.EndDate, day))
            .Sum(d => d.HoursCount);
    }

    /// <summary>
    ///     4 კვირის საათები = 4 × კოეფიციენტი × კვირის საათები
    /// </summary>
    public static float FourWeekHours(float hoursCoefficient, float weeklyHours)
    {
        return WeeksInFourWeeks * hoursCoefficient * weeklyHours;
    }

    /// <summary>
    ///     სტრიქონის 4 კვირის საათები ფორმულას ესადაგება: ფორმულის მნიშვნელობისგან GeneratorHours-ის ზღვარზე მეტად არ
    ///     განსხვავდება
    /// </summary>
    public static bool HoursMatchSchedule(float fourWeekHours, float hoursCoefficient, float weeklyHours)
    {
        return !GeneratorHours.AreDifferent(fourWeekHours, FourWeekHours(hoursCoefficient, weeklyHours));
    }

    /// <summary>
    ///     |4 კვირის გადასახადი − საათის ფასი × 4 კვირის საათები| ≤ 0.01
    /// </summary>
    public static bool FeeMatchesHours(decimal fourWeekFee, decimal oneHourFee, float fourWeekHours)
    {
        return Math.Abs(fourWeekFee - oneHourFee * SingleToDecimal(fourWeekHours)) <= FeeTolerance;
    }

    //float 7 მნიშვნელოვან ციფრამდე, როგორც r30-ში (MismatchReports.SingleToDecimal): .NET 11-ის ზუსტი ორობითი
    //გარდაქმნით (6.6000004f → 6.60000038146972…) ზუსტად 0.01-იანი სხვაობა ზღვრის ერთ მხარეს გადასცდებოდა
    private static decimal SingleToDecimal(float value)
    {
        return decimal.Parse(value.ToString("G7", CultureInfo.InvariantCulture), NumberStyles.Float,
            CultureInfo.InvariantCulture);
    }
}
