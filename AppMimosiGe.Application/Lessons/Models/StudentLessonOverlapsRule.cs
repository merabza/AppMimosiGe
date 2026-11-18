using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     მოსწავლეების დროების გადაფარვა გაკვეთილებში (D179). ჟურნალი არ ინახავს სტატუსის ან აღდგენის თარიღის ცვლილებას,
///     რომლის შემდეგაც გაკვეთილის რომელიმე მოსწავლეს იმავე დროს სხვა ჩასატარებელი (სტატუსი "არ გაუქმებულა") გაკვეთილი
///     ექნება. გაკვეთილის დრო იგივეა, რაც მასწავლებლის წესში (TeacherLessonOverlapsRule.TeacherTime): ფაქტობრივი
///     დაწყება + მოსწავლეების საათების მაქსიმუმი; დასწრება არ ითვლება. დრო თუ არ იცვლება (შემცვლელი, დასწრება ან სხვა
///     ველი), არაფერი მოწმდება, ამიტომ უკვე შენახული გადაფარვა სხვა ცვლილებას არ აბრკოლებს. ჯგუფების განრიგის
///     გადაფარვა (r19) GroupScheduleOverlapsRule-ისაა
/// </summary>
public static class StudentLessonOverlapsRule
{
    //მოსწავლეების გაკვეთილები, რომლებიც [start, end) დროს მიმდინარეობს: დაწყების, შემდეგ მოსწავლის რიგით
    public static IReadOnlyList<StudentLesson> Overlapping(DateTime start, DateTime end,
        IEnumerable<StudentLesson> lessons)
    {
        return
        [
            .. lessons.Where(l => l.Start < end && start < l.End).OrderBy(l => l.Start)
                .ThenBy(l => l.StudentContractId).ThenBy(l => l.LessonId)
        ];
    }
}
