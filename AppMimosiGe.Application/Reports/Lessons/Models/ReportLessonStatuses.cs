namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     გაკვეთილის სტატუსები, რომლებსაც რეპორტები ეყრდნობა (LessonStatuses-ის ID-ები; D36: მათ generic-ით ვერ შექმნი
///     და ვერ წაშლი). სტატუსი 3 („გაუქმდა მასწავლებლისგან დამოუკიდებელი მიზეზების გამო") Access-ის query-ებში
///     მხოლოდ "არც 1, არც 2"-ად ჩანს: დარიცხვასა და ხელფასში ითვლება
/// </summary>
public static class ReportLessonStatuses
{
    //"არ გაუქმებულა"
    public const int NotCancelled = 1;

    //"გაუქმდა"
    public const int Cancelled = 2;
}
