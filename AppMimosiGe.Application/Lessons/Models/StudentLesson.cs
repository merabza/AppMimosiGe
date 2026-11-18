using System;
using System.Globalization;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     მოსწავლის ჩასატარებელი გაკვეთილი (StudentLessonOverlapsRule, D179): მოსწავლის კონტრაქტი, გაკვეთილი, ჯგუფი,
///     ფაქტობრივი დაწყება და საათები (გაკვეთილის მოსწავლეების საათების მაქსიმუმი, როგორც TeacherLesson-ში)
/// </summary>
public sealed record StudentLesson(int StudentContractId, int LessonId, string GroupCode, DateTime Start, float Hours)
{
    public DateTime End => Start.AddHours(Hours);

    //"მოსწავლე — ჯგუფი კოდი, 30.09.2026 15:00–16:30"
    public string Describe(string studentName)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{studentName} — ჯგუფი {GroupCode}, {Start:dd.MM.yyyy HH:mm}–{End:HH:mm}");
    }
}
