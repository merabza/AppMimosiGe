namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გენერატორის შეცდომების კოდები (ErrorLogTexts-ის ID-ები). 4 (მოსწავლე არ არის), 8 და 13 (შეტანილი მონაცემის გამო
///     შეუცვლელი გაკვეთილი ან საათები) Access-შიც არ იწერებოდა. 9, 10 და 12 Access-ში ბაზის ცალკეული ბრძანების ჩავარდნას
///     აღნიშნავდა; აქ ჯგუფის ცვლილებები ერთ ტრანზაქციაში ინახება და ჩავარდნისას მთლიანად უქმდება, ამიტომ ეს კოდები არ იწერება
/// </summary>
public static class LessonGeneratorErrorCodes
{
    public const int NoTeachers = 1;
    public const int NoStudents = 2;
    public const int NoDayTimePlaces = 3;
    public const int TwoTeachersOnDay = 5;
    public const int NoTeacherOnDay = 6;
    public const int TwoDayTimePlacesOnDay = 7;
    public const int ExtraLessonHasEnteredData = 11;
    public const int ExtraStudentHasEnteredData = 14;
}
