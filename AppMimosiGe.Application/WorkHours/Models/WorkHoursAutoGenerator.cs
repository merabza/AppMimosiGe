using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     სამუშაო დროის ავტომატური დაგენერირება (Access-ის vCountedWorkHours და FrmWorkHours.cmdAutoGenerate-ის INSERT).
///     ყოველი დღისთვის, როცა ცენტრში გაკვეთილი იყო, და ყოველი თანამშრომლისთვის: დაწყება = დღე + კონტრაქტის
///     ფიქსირებული დაწყება, თუ არა, დღის პირველი გაკვეთილის დაწყება მთელ ცენტრში; დასრულება = დღე + ფიქსირებული
///     დასრულება, თუ არა, ბოლო გაკვეთილის დასრულება. Access-ის query-ში კონტრაქტი და გაკვეთილები ერთმანეთს არ ებმის:
///     ეს ცენტრის სამუშაო დღეა და არა თანამშრომლის გაკვეთილები
/// </summary>
public static class WorkHoursAutoGenerator
{
    //lessonTimes: გაუქმებული გაკვეთილების გარდა ყველა გაკვეთილის მოსწავლის სტრიქონი (Access-ის INNER JOIN
    //LessonsByStudents: მოსწავლის გარეშე გაკვეთილი დღეს არ ქმნის). dateFrom და dateTo დღეებია, ორივე ჩათვლით.
    //ჩანაწერი იქმნება, თუ: დღე დღევანდელზე ადრეა, დაწყება პერიოდშია, დაწყება დასრულებაზე ადრეა, თანამშრომელს
    //ამ დღეს ჩანაწერი ჯერ არ აქვს და (Access-ისგან განსხვავებით) კონტრაქტი ამ დღეს მოქმედებს
    public static List<GeneratedWorkHour> Plan(IReadOnlyCollection<WorkHourEmployee> employees,
        IEnumerable<LessonTimeData> lessonTimes, IEnumerable<WorkHourDay> existingDays, DateTime dateFrom,
        DateTime dateTo, DateTime today)
    {
        DateTime periodStart = dateFrom.Date;
        DateTime periodEnd = dateTo.Date.AddDays(1);
        HashSet<(int, DateTime)> existing = [.. existingDays.Select(d => (d.TeacherContractId, d.Day.Date))];

        List<GeneratedWorkHour> generated = [];
        foreach (IGrouping<DateTime, LessonTimeData> lessonDay in lessonTimes.GroupBy(l => l.LessonDt.Date)
                     .Where(g => g.Key < today.Date).OrderBy(g => g.Key))
        {
            DateTime day = lessonDay.Key;
            DateTime firstLessonStart = lessonDay.Min(l => l.LessonDt);
            //Access-ის DateAdd("h", HoursCount, LessonDT) წილად საათს ჭრიდა (1.5 → 1); აქ ზუსტი დასრულებაა
            DateTime lastLessonEnd = lessonDay.Max(l => l.LessonDt.AddHours(l.HoursCount));

            foreach (WorkHourEmployee employee in employees.Where(e => e.IsActiveOn(day)).OrderBy(e => e.Id))
            {
                DateTime start = employee.WorkHoursStart is { } fixedStart
                    ? day + fixedStart.TimeOfDay
                    : firstLessonStart;
                DateTime end = employee.WorkHoursEnd is { } fixedEnd ? day + fixedEnd.TimeOfDay : lastLessonEnd;

                if (start >= periodStart && start < periodEnd && start < end && !existing.Contains((employee.Id, day)))
                {
                    generated.Add(new GeneratedWorkHour(employee.Id, start, end));
                }
            }
        }

        return generated;
    }
}
