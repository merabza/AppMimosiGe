using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Lessons.UpdateLesson;

/// <summary>
///     ჟურნალის რედაქტირებადი ველები entity-ზე. ჯგუფს, მასწავლებელს, დროს, სქემას და საათებს გენერატორი ადგენს და
///     აქ არ იცვლება. ცარიელი ტექსტი NULL-ად ინახება: გენერატორი ცარიელ სტრიქონსაც შეტანილ მონაცემად თვლის (D65, Q17)
/// </summary>
public static class LessonMapper
{
    public static void ApplyFields(Lesson lesson, LessonRequest request)
    {
        lesson.LessonStatusId = request.LessonStatusId;
        lesson.SubstituteTeacherContractId = request.SubstituteTeacherContractId;
        lesson.TeacherLateMinutes = request.TeacherLateMinutes;
        lesson.RecoverDate = request.RecoverDate?.Date;
        lesson.Note = NormalizeText(request.Note);
    }

    public static void ApplyStudentFields(LessonByStudent row, LessonStudentRequest request)
    {
        row.Present = request.Present;
        row.Theme = NormalizeText(request.Theme);
        row.Rate = request.Rate;
        row.TeacherComment = NormalizeText(request.TeacherComment);
        row.StudentComment = NormalizeText(request.StudentComment);
        row.StudentLateMinutes = request.StudentLateMinutes;
    }

    public static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
