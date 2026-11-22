using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის აქტიურობა (D200): ჯგუფი მოქმედია d დღეს, თუ ამ დღეს გაუქმებული არ არის (VoidDate &gt; d) და მასში
///     მოქმედებს მასწავლებლის, მოსწავლის და განრიგის თითო სტრიქონი მაინც ([StartDate, EndDate) შეიცავს d-ს). სხვა დღეებში
///     ჯგუფი უმოქმედოა. ეს რეპორტების ActiveGroupsForReports-ის (Infrastructure, D116) წესია, იგივე შედარებებით, ამიტომ
///     თარიღების დროს მნიშვნელობა არ აქვს
/// </summary>
public static class GroupActivity
{
    /// <summary>
    ///     ჯგუფი მოქმედია თუ არა day დღეს
    /// </summary>
    public static bool IsActiveOn(GroupState state, DateTime day)
    {
        DateTime dayAfter = day.Date.AddDays(1);
        return (state.VoidDate is null || state.VoidDate >= dayAfter) &&
               state.Teachers.Any(t => IsActiveRow(t.StartDate, t.EndDate, dayAfter)) &&
               state.Students.Any(s => IsActiveRow(s.StartDate, s.EndDate, dayAfter)) &&
               state.DayTimePlaces.Any(d => IsActiveRow(d.StartDate, d.EndDate, dayAfter));
    }

    /// <summary>
    ///     ჯგუფის მოქმედი პერიოდები დროის რიგით; ერთმანეთის მომდევნო მოქმედი დღეები ერთ პერიოდშია
    /// </summary>
    public static IReadOnlyList<GroupActivePeriod> ActivePeriods(GroupState state)
    {
        //აქტიურობა მხოლოდ იმ დღეს იცვლება, როცა სტრიქონი იწყება ან სრულდება, ან ჯგუფი უქმდება. ასეთ დღეებს შორის ის
        //უცვლელია, პირველ ასეთ დღემდე კი ჯგუფი უმოქმედოა, რადგან არცერთი სტრიქონი ჯერ არ დაწყებულა
        List<GroupActivePeriod> periods = [];
        DateTime? periodStart = null;
        foreach (DateTime day in ChangeDays(state))
        {
            bool isActive = IsActiveOn(state, day);
            if (isActive && periodStart is null)
            {
                periodStart = day;
            }
            else if (!isActive && periodStart is { } start)
            {
                periods.Add(new GroupActivePeriod(start, day));
                periodStart = null;
            }
        }

        if (periodStart is { } lastStart)
        {
            periods.Add(new GroupActivePeriod(lastStart, null));
        }

        return periods;
    }

    /// <summary>
    ///     ჯგუფის პირველი მოქმედი დღე, წარსულში ან მომავალში (D196); null, თუ ჯგუფი არცერთ დღეს არ არის მოქმედი
    /// </summary>
    public static DateTime? FirstActiveDay(GroupState state)
    {
        return ActivePeriods(state) is [GroupActivePeriod first, ..] ? first.StartDate : null;
    }

    //ActiveGroupsForReports-ის პირობა: სტრიქონი d დღეს მოქმედებს, თუ StartDate < d + 1 და EndDate >= d + 1
    private static bool IsActiveRow(DateTime startDate, DateTime? endDate, DateTime dayAfter)
    {
        return startDate < dayAfter && (endDate is null || endDate >= dayAfter);
    }

    //დღეები, როცა აქტიურობა შეიძლება შეიცვალოს, ზრდადობით
    private static IEnumerable<DateTime> ChangeDays(GroupState state)
    {
        IEnumerable<DateTime?> dates =
        [
            state.VoidDate,
            .. state.Teachers.SelectMany(t => new DateTime?[] { t.StartDate, t.EndDate }),
            .. state.Students.SelectMany(s => new DateTime?[] { s.StartDate, s.EndDate }),
            .. state.DayTimePlaces.SelectMany(d => new DateTime?[] { d.StartDate, d.EndDate })
        ];
        return dates.OfType<DateTime>().Select(d => d.Date).Distinct().Order();
    }
}
