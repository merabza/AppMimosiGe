using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     მასწავლებლის დროების გადაფარვა გაკვეთილებში (D178). გაკვეთილს ატარებს შემცვლელი, თუ არის, თორემ მასწავლებელი;
///     ჩასატარებელია მხოლოდ სტატუსით "არ გაუქმებულა"; დრო = ფაქტობრივი დაწყება (აღდგენის დღე, თუ აქვს, გაკვეთილის
///     დროით) + საათები (მოსწავლეების საათების მაქსიმუმი), როგორც r36-ში. ჟურნალი არ ინახავს შემცვლელის, სტატუსის ან
///     აღდგენის თარიღის ცვლილებას, რომლის შემდეგაც გაკვეთილის ჩამტარებელს იმავე დროს სხვა ჩასატარებელი გაკვეთილი ექნება.
///     შეუცვლელი ჩამტარებელი და დრო არ მოწმდება, ამიტომ უკვე შენახული გადაფარვა სხვა ცვლილებას არ აბრკოლებს. ჯგუფების
///     განრიგის გადაფარვა (r18) GroupScheduleOverlapsRule-ისაა, გაკვეთილის მოსწავლეებისა StudentLessonOverlapsRule-ის
/// </summary>
public static class TeacherLessonOverlapsRule
{
    /// <summary>
    ///     გაკვეთილის ჩამტარებელი და დრო; null, თუ გაკვეთილი ჩასატარებელი არ არის ან საათები არ აქვს
    /// </summary>
    public static LessonTeacherTime? TeacherTime(int lessonStatusId, int teacherContractId,
        int? substituteTeacherContractId, DateTime lessonDt, DateTime? recoverDate, float hours)
    {
        if (!LessonVoidStatusRule.AllowsPresentStudentsAndRecoverDate(lessonStatusId) || hours <= 0)
        {
            return null;
        }

        return new LessonTeacherTime(substituteTeacherContractId ?? teacherContractId,
            ActualStart(lessonDt, recoverDate), hours);
    }

    //აღდგენილი გაკვეთილი აღდგენის დღეს ტარდება, გაკვეთილის დროს (r36)
    public static DateTime ActualStart(DateTime lessonDt, DateTime? recoverDate)
    {
        return recoverDate is { } recoverDay ? recoverDay.Date + lessonDt.TimeOfDay : lessonDt;
    }

    //დღეები [From, To), რომელთა გაკვეთილებს შეუძლია ამ დროის გადაფარვა (გაკვეთილი დღეზე მოკლეა)
    public static (DateTime From, DateTime To) SearchDays(LessonTeacherTime time)
    {
        return (time.Start.Date.AddDays(-1), time.End.Date.AddDays(1));
    }

    //გაკვეთილები, რომლებიც ამ დროს მიმდინარეობს, დაწყების რიგით
    public static IReadOnlyList<TeacherLesson> Overlapping(LessonTeacherTime time, IEnumerable<TeacherLesson> lessons)
    {
        return
        [
            .. lessons.Where(l => l.Start < time.End && time.Start < l.End).OrderBy(l => l.Start)
                .ThenBy(l => l.LessonId)
        ];
    }
}
