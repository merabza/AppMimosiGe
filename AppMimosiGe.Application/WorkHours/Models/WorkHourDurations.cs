using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     ნამუშევარი დროის ხანგრძლივობა საათებში, 2 ათწილადით (როგორც r36-ის Round(... * 24, 2)). დაუსრულებელ ჩანაწერს
///     ხანგრძლივობა არ აქვს და ჯამში არ ითვლება (Access-ის Sum Null-ს გამოტოვებს)
/// </summary>
public static class WorkHourDurations
{
    public static decimal? Hours(DateTime whStart, DateTime? whEnd)
    {
        return whEnd is { } end ? ToHours((end - whStart).Ticks) : null;
    }

    //თითო თანამშრომელზე: დასრულებული ჩანაწერების ზუსტი ხანგრძლივობების ჯამი (დამრგვალება ბოლოს) და ყველა ჩანაწერის
    //რაოდენობა; სახელით დალაგებული. სახელი კონტრაქტით განისაზღვრება, ამიტომ ორივეთი დაჯგუფება იგივეა, რაც კონტრაქტით
    public static List<WorkHoursTotalResponse> Totals(IEnumerable<WorkHourTimeData> records)
    {
        return
        [
            .. records.GroupBy(r => (r.TeacherContractId, r.EmployeeName)).Select(g => new WorkHoursTotalResponse(
                    g.Key.TeacherContractId, g.Key.EmployeeName,
                    ToHours(g.Where(r => r.WhEnd is not null).Sum(r => (r.WhEnd!.Value - r.WhStart).Ticks)), g.Count()))
                .OrderBy(t => t.EmployeeName, StringComparer.Ordinal).ThenBy(t => t.TeacherContractId)
        ];
    }

    private static decimal ToHours(long ticks)
    {
        return Math.Round((decimal)ticks / TimeSpan.TicksPerHour, 2);
    }
}
