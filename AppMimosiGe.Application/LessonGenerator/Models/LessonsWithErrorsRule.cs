using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     შეცდომიანი გაკვეთილების წესი (რეპორტი r13 "შეცდომიანი გაკვეთილები", D120): მოსწავლის სტრიქონი შეტანილი
///     მონაცემით (D65), რომელსაც გენერატორი ზედმეტად თვლის და ამიტომ ვეღარ შლის. ზედმეტი შეიძლება იყოს მთელი გაკვეთილი
///     (შეცდომა 11, მაშინ შეცდომიანია მისი ყველა სტრიქონი შეტანილი მონაცემით) ან მოსწავლე გაკვეთილში (შეცდომა 14).
///     ზედმეტს GroupLessonsPlanner ადგენს ჯგუფის მასწავლებლებით, მოსწავლეებით, განრიგით, გაუქმების თარიღით და
///     ჰორიზონტით, ისევე როგორც გენერაციისას. ჯგუფის და ჟურნალის რედაქტორები არ ინახავენ ცვლილებას, რომელიც ახალ
///     შეცდომიან სტრიქონს ქმნის. უკვე არსებული შეცდომა სხვა ცვლილებას არ აბრკოლებს და r13-ში ჩანს (D177)
/// </summary>
public static class LessonsWithErrorsRule
{
    /// <summary>
    ///     სტრიქონები, რომლებიც ცვლილების შემდეგ (after) შეცდომიანია, ცვლილებამდე (before) კი არ იყო. before და after ერთი
    ///     ჯგუფია ერთი და იმავე გაკვეთილებით: იცვლება ჯგუფის ველები და სტრიქონები ან მოსწავლეების შეტანილი მონაცემი
    /// </summary>
    public static IReadOnlyList<ErroneousLessonStudent> NewErrors(GroupLessonsInput before, GroupLessonsInput after,
        DateTime horizonEnd)
    {
        HashSet<int> erroneousRowIds = [.. Errors(before, horizonEnd).Select(e => e.LessonStudentId)];
        return [.. Errors(after, horizonEnd).Where(e => !erroneousRowIds.Contains(e.LessonStudentId))];
    }

    //გენერატორის შეცდომები 11 (ზედმეტი გაკვეთილის ყველა სტრიქონი შეტანილი მონაცემით) და 14 (ზედმეტი მოსწავლის
    //სტრიქონი) მოსწავლეების სტრიქონებად
    private static IEnumerable<ErroneousLessonStudent> Errors(GroupLessonsInput input, DateTime horizonEnd)
    {
        Dictionary<int, ExistingLesson> lessons = input.Lessons.ToDictionary(l => l.Id);
        foreach (PlannedLogEntry log in GroupLessonsPlanner.PlanGroup(input, horizonEnd).Logs.Where(l =>
                     l.ErrorCode is LessonGeneratorErrorCodes.ExtraLessonHasEnteredData
                         or LessonGeneratorErrorCodes.ExtraStudentHasEnteredData))
        {
            ExistingLesson lesson = lessons[log.LessonId!.Value];
            foreach (ExistingLessonStudent row in lesson.Students.Where(s =>
                         s.HasEnteredData && (log.ErrorCode == LessonGeneratorErrorCodes.ExtraLessonHasEnteredData ||
                                              s.Id == log.LessonStudentId)))
            {
                yield return new ErroneousLessonStudent(log.ErrorCode, lesson.Id, lesson.Values.LessonDt, row.Id,
                    row.StudentContractId);
            }
        }
    }
}
