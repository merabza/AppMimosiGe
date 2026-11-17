using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
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

    /// <summary>
    ///     გაკვეთილების გენერატორის შემავალი მონაცემები ჟურნალის შენახვის შემდეგ (შეცდომიანი გაკვეთილების
    ///     შესამოწმებლად, D177): lessonId-ის მოსწავლეების შეტანილი მონაცემი მოთხოვნიდან, როგორც მას ApplyStudentFields
    ///     შეინახავს. მოთხოვნიდან გამოტოვებული სტრიქონი და ჯგუფის სხვა გაკვეთილები უცვლელია
    /// </summary>
    public static GroupLessonsInput ToLessonsInput(GroupLessonsInput current, int lessonId, LessonRequest request)
    {
        Dictionary<int, LessonStudentRequest> rows = request.Students.ToDictionary(x => x.Id);
        return current with
        {
            Lessons =
            [
                .. current.Lessons.Select(lesson => lesson.Id != lessonId
                    ? lesson
                    : lesson with
                    {
                        Students =
                        [
                            .. lesson.Students.Select(student =>
                                rows.TryGetValue(student.Id, out LessonStudentRequest? row)
                                    ? student with { HasEnteredData = HasEnteredData(row) }
                                    : student)
                        ]
                    })
            ]
        };
    }

    public static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    //შეტანილი მონაცემი ისე, როგორც სტრიქონი შეინახება (ცარიელი ტექსტი NULL ხდება)
    private static bool HasEnteredData(LessonStudentRequest request)
    {
        var row = new LessonByStudent();
        ApplyStudentFields(row, request);
        return LessonGeneratorMapper.HasEnteredData(row);
    }
}
