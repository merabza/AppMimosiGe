namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     გაკვეთილის სტატუსის წესი (Access-ის vR12LessonsWithWrongVoidStatus, რეპორტი r12 "არასწორად გაუქმებული
///     გაკვეთილები"): სტატუსი "არ გაუქმებულა"-ს გარდა ნიშნავს, რომ გაკვეთილი არ ჩატარდა, ამიტომ მას დამსწრე მოსწავლე
///     და აღდგენის თარიღი არ უნდა ჰქონდეს. რეპორტი ამ წესით ეძებს არასწორ გაკვეთილებს, ჟურნალის რედაქტორი კი მათ
///     შენახვას არ უშვებს. გენერატორი სტატუსს, დასწრებას და აღდგენის თარიღს არ ცვლის
/// </summary>
public static class LessonVoidStatusRule
{
    //"არ გაუქმებულა"
    public const int NotCancelledStatusId = 1;

    public static bool AllowsPresentStudentsAndRecoverDate(int lessonStatusId)
    {
        return lessonStatusId == NotCancelledStatusId;
    }

    public static bool IsBroken(int lessonStatusId, bool hasPresentStudent, bool hasRecoverDate)
    {
        return !AllowsPresentStudentsAndRecoverDate(lessonStatusId) && (hasPresentStudent || hasRecoverDate);
    }
}
