using System;
using System.Globalization;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     მასწავლებლის ჩასატარებელი გაკვეთილი (TeacherLessonOverlapsRule, D178): ჯგუფი, ფაქტობრივი დაწყება და საათები
/// </summary>
public sealed record TeacherLesson(int LessonId, string GroupCode, DateTime Start, float Hours)
{
    public DateTime End => Start.AddHours(Hours);

    //"ჯგუფი კოდი, 30.09.2026 15:00–16:30"
    public string Describe()
    {
        return string.Create(CultureInfo.InvariantCulture, $"ჯგუფი {GroupCode}, {Start:dd.MM.yyyy HH:mm}–{End:HH:mm}");
    }
}
