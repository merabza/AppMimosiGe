using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     crosstab, Access-ის TRANSFORM … PIVOT WeekDaies.ShortName-ის მსგავსად: სტრიქონი გასაღებია, სვეტი კვირის დღე.
///     სვეტების რიგი ფიქსირებულია: WeekDays-ის რიგი, ყოველთვის შვიდივე დღე (Access-შიც RIGHT JOIN WeekDaies ყველა დღეს
///     სვეტად აქცევდა). უჯრა, რომელშიც ჩანაწერი არ არის, null-ია. სტრიქონების რიგს გამომძახებელი ადგენს
/// </summary>
public static class WeekDayPivot
{
    public static List<PivotRow<TKey>> Pivot<TItem, TKey>(IEnumerable<TItem> items, Func<TItem, TKey> keySelector,
        Func<TItem, int> weekDaySelector, Func<IReadOnlyCollection<TItem>, object> aggregate,
        IReadOnlyList<ScheduleWeekDay> weekDays) where TKey : notnull
    {
        return
        [
            .. items.GroupBy(keySelector).Select(row => new PivotRow<TKey>(row.Key, [
                .. weekDays.Select(day =>
                {
                    List<TItem> cell = [.. row.Where(item => weekDaySelector(item) == day.WeekDayId)];
                    return cell.Count == 0 ? null : aggregate(cell);
                })
            ]))
        ];
    }

    //კვირის დღეების სვეტები WeekDays-ის რიგით, სათაური დღის მოკლე სახელია ("1-ორ" … "7-კვ")
    public static List<ReportColumnResponse> WeekDayColumns(IReadOnlyList<ScheduleWeekDay> weekDays, string type)
    {
        return [.. weekDays.Select(day => new ReportColumnResponse($"weekDay{day.WeekDayId}", day.ShortName, type))];
    }
}
