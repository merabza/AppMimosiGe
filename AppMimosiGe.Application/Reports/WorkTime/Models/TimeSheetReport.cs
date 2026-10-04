using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.WorkTime.Models;

/// <summary>
///     სამუშაო დროის აღრიცხვის ფორმა (r36): თანამშრომლების ნამუშევარი საათები თვის დღეების მიხედვით
/// </summary>
public static class TimeSheetReport
{
    private const int MonthDays = 31;
    private const int FirstHalfDays = 15;
    private const int HoursDecimals = 2;

    //r36 (VR36Base1–VR36Base7, crosstab): დღის საათები = გაკვეთილებისა და სამუშაო საათების ჩანაწერების დროის
    //შუალედების გაერთიანება, ერთდროული დრო ერთხელ ითვლება (Access ორივეს აჯამებდა, UNION კი ერთნაირ რიცხვებს
    //აერთიანებდა). დღე = შუალედის დაწყების დღე. საათები 2 ათწილადით (Access მთელამდე ამრგვალებდა). სექცია = თვე
    //(Access სხვადასხვა თვის ერთსა და იმავე რიცხვს ერთ სვეტში აჯამებდა); სტრიქონი = თანამშრომელი, ვისაც ამ თვეში
    //საათები აქვს: რიგითი ნომერი, "გვარი სახელი", კონტრაქტის ნომერი (ფორმის "ტაბელის ნომერი"), დღეები 1–15, I
    //ნახევრის ჯამი, დღეები 16–31, II ნახევრის ჯამი, ნამუშევარი დღეები და საათები (Access-ში ჯამები ცარიელი იყო).
    //რიგი: სახელი, ნომერი
    public static ReportTable TimeSheet(WorkTimeData data, IReadOnlyDictionary<int, string> monthNames)
    {
        List<WorkDay> days =
        [
            .. Intervals(data).GroupBy(i => (i.EmployeeId, Day: i.Start.Date))
                .Select(day => new WorkDay(day.Key.EmployeeId, day.Key.Day, UnionHours(day)))
        ];
        return new ReportTable(Columns(), [
            .. days.GroupBy(d => ReportMonths.Start(d.Day)).OrderBy(month => month.Key).Select(month =>
                new ReportSectionResponse(ReportMonths.Name(month.Key, monthNames), [
                    .. month.GroupBy(d => d.EmployeeId).Select(employee => (Person: data.Employees[employee.Key],
                            Hours: employee.ToDictionary(d => d.Day.Day, d => d.Hours)))
                        .OrderBy(e => e.Person.FullName, StringComparer.Ordinal)
                        .ThenBy(e => e.Person.ContractNumber, StringComparer.Ordinal)
                        .Select((e, index) => EmployeeRow(index + 1, e.Person, e.Hours))
                ], null))
        ], []);
    }

    //№, გვარი სახელი, ნომერი, 1–15, I ნახევარი, 16–31, II ნახევარი, დღე, საათი
    private static List<ReportColumnResponse> Columns()
    {
        return
        [
            new ReportColumnResponse("number", "№", ReportColumnTypes.WholeNumber),
            new ReportColumnResponse("employee", "გვარი, სახელი", ReportColumnTypes.Text),
            new ReportColumnResponse("contractNumber", "ტაბელის ნომერი", ReportColumnTypes.Text),
            .. Enumerable.Range(1, FirstHalfDays).Select(DayColumn),
            new ReportColumnResponse("firstHalf", "I ნახევარი", ReportColumnTypes.Number),
            .. Enumerable.Range(FirstHalfDays + 1, MonthDays - FirstHalfDays).Select(DayColumn),
            new ReportColumnResponse("secondHalf", "II ნახევარი", ReportColumnTypes.Number),
            new ReportColumnResponse("days", "სულ დღე", ReportColumnTypes.WholeNumber),
            new ReportColumnResponse("hours", "სულ საათი", ReportColumnTypes.Number)
        ];
    }

    private static ReportColumnResponse DayColumn(int day)
    {
        string number = day.ToString(CultureInfo.InvariantCulture);
        return new ReportColumnResponse("day" + number, number, ReportColumnTypes.Number);
    }

    //თანამშრომლის სტრიქონი: დღე, რომელშიც საათები არ აქვს, ცარიელია; ჯამები დღეების (დამრგვალებული) საათებისაა
    private static List<object?> EmployeeRow(int number, SchedulePerson person, Dictionary<int, decimal> hours)
    {
        decimal firstHalf = hours.Where(h => h.Key <= FirstHalfDays).Sum(h => h.Value);
        decimal secondHalf = hours.Where(h => h.Key > FirstHalfDays).Sum(h => h.Value);
        return
        [
            number, person.FullName, person.ContractNumber,
            .. Enumerable.Range(1, FirstHalfDays).Select(day => DayHours(hours, day)), firstHalf,
            .. Enumerable.Range(FirstHalfDays + 1, MonthDays - FirstHalfDays).Select(day => DayHours(hours, day)),
            secondHalf, hours.Count(h => h.Value > 0), firstHalf + secondHalf
        ];
    }

    private static decimal? DayHours(Dictionary<int, decimal> hours, int day)
    {
        return hours.TryGetValue(day, out decimal value) ? value : null;
    }

    //გაკვეთილი: [დაწყება, დაწყება + საათები), აღდგენილი გაკვეთილი აღდგენის დღეს თავისი დაწყების დროით; ჩანაწერი:
    //[დაწყება, დასრულება). ცარიელი ან შებრუნებული შუალედი არ ითვლება
    private static IEnumerable<WorkInterval> Intervals(WorkTimeData data)
    {
        return data.Lessons.Select(l =>
            {
                DateTime start = l.RecoverDate is { } recoverDate ? recoverDate.Date + l.LessonDt.TimeOfDay : l.LessonDt;
                return new WorkInterval(l.EmployeeId, start, start.AddHours(l.Hours));
            }).Concat(data.Records.Select(r => new WorkInterval(r.EmployeeId, r.Start, r.End)))
            .Where(i => i.End > i.Start);
    }

    //შუალედების გაერთიანების ხანგრძლივობა საათებში, 2 ათწილადით (როგორც სამუშაო საათების სიაში, D102). დაწყებით
    //დალაგებულ შუალედებში ითვლება მხოლოდ უკვე დაფარული დროის შემდეგი ნაწილი (დაფარულის შიგნით მყოფი შუალედი — 0)
    private static decimal UnionHours(IEnumerable<WorkInterval> intervals)
    {
        long ticks = 0;
        DateTime coveredUntil = DateTime.MinValue;
        foreach (WorkInterval interval in intervals.OrderBy(i => i.Start))
        {
            DateTime[] starts = [interval.Start, coveredUntil];
            DateTime[] ends = [interval.End, coveredUntil];
            DateTime end = ends.Max();
            ticks += (end - starts.Max()).Ticks;
            coveredUntil = end;
        }

        return Math.Round((decimal)ticks / TimeSpan.TicksPerHour, HoursDecimals);
    }

    private sealed record WorkInterval(int EmployeeId, DateTime Start, DateTime End);

    private sealed record WorkDay(int EmployeeId, DateTime Day, decimal Hours);
}
