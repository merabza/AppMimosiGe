using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     გაკვეთილების ჟურნალის შემოწმებები: გასაუქმებელი (r11) და არასწორად გაუქმებული (r12) გაკვეთილები, გენერატორის
///     შეცდომიანი გაკვეთილები (r13), თეორიული თარიღები დროის გარეშე (r22), გაუქმებები და ჩანაცვლებები (r34)
/// </summary>
public static class LessonCheckReports
{
    private const string CasesCaption = "შემთხვევების რაოდენობა:";
    private const string TeacherCaption = "მასწავლებელი";

    //r11WrongStatuseLessons (vR11WrongStatuseLessons, vLessonsWithPresentStudents): სტატუსი "არ გაუქმებულა" და
    //არცერთი დამსწრე მოსწავლე (მოსწავლის გარეშე გაკვეთილიც). პერიოდს და "მხოლოდ დაწყებულს" (D121) handler ზღუდავს
    public static ReportTable WrongStatusLessons(IEnumerable<PeriodLessonRow> lessons)
    {
        return LessonsList(lessons.Where(l =>
            l.LessonStatusId == ReportLessonStatuses.NotCancelled && !l.HasPresentStudent));
    }

    //r12LessonsWithWrongVoidStatus (vR12LessonsWithWrongVoidStatus): სტატუსი "არ გაუქმებულა"-ს გარდა (2 ან 3) და
    //დამსწრე მოსწავლე ან აღდგენის თარიღი
    public static ReportTable LessonsWithWrongVoidStatus(IEnumerable<PeriodLessonRow> lessons)
    {
        return LessonsList(lessons.Where(l =>
            l.LessonStatusId != ReportLessonStatuses.NotCancelled && (l.HasPresentStudent || l.HasRecoverDate)));
    }

    //r13LessonsWithErrors (vR13LessonsWithErrors): გენერატორის ლოგის ჩანაწერები, რომლებსაც ამავე ჯგუფის გაკვეთილი
    //აქვს (D120). რიგი: ჯგუფი, გაკვეთილის დრო (Access-ის რეპორტის დაჯგუფება)
    public static ReportTable LessonsWithErrors(IEnumerable<LessonErrorRow> errors)
    {
        return ReportTable.Flat([
            Text("groupCode", "ჯგუფი"), DateTimeColumn("lessonDt", "გაკვეთილის თარიღი"),
            Text("errorText", "შეცდომის ტექსტი")
        ], [
            .. errors.OrderBy(e => e.GroupCode, StringComparer.Ordinal).ThenBy(e => e.LessonDt).ThenBy(e => e.LogId)
                .Select(e => new List<object?> { e.GroupCode, e.LessonDt, e.ErrorText })
        ]);
    }

    //r22 (vR22): გაკვეთილები, რომელთა TeoMinDate-ის ან TeoMaxDate-ის დრო 00:00:00-ია (Access-ის
    //Format(…, "hh:nn:ss") = "00:00:00"; ამას რეპოზიტორია ფილტრავს). Access-ის query-ს რიგი არ ჰქონდა: ჯგუფი,
    //გაკვეთილის დრო. ბოლოს Access-ის რეპორტის ჩანაწერების რაოდენობა
    public static ReportTable WrongWeekDayChanges(IEnumerable<TeoDatesLessonRow> lessons)
    {
        List<ReportColumnResponse> columns =
        [
            Text("groupCode", "ჯგუფი"), Text("teacher", "მასწავლებელი"),
            DateTimeColumn("teoMinDate", "თეორიულად მინიმალური თარიღი"),
            DateTimeColumn("teoMaxDate", "თეორიულად მაქსიმალური თარიღი"),
            new("fourWeekHours", "4 კვირაში საათები", ReportColumnTypes.Number),
            DateTimeColumn("lessonDt", "ჩატარების თარიღი და დრო")
        ];
        List<List<object?>> rows =
        [
            .. lessons.OrderBy(l => l.GroupCode, StringComparer.Ordinal).ThenBy(l => l.LessonDt).ThenBy(l => l.LessonId)
                .Select(l => new List<object?>
                {
                    l.GroupCode,
                    l.Teacher.NameWithNumber,
                    l.TeoMinDate,
                    l.TeoMaxDate,
                    l.FourWeekHours,
                    l.LessonDt
                })
        ];
        return ReportTable.Flat(columns, rows, [ReportFooters.Count(columns, rows.Count)]);
    }

    //R34TeacherMissAndSubstitutes (vR34TeacherMissAndSubstitutes): სტატუსი "გაუქმდა" ან შემცვლელი მასწავლებელი.
    //სექცია = გაკვეთილის მასწავლებელი (Access-ის ჯგუფის სათაური), სტრიქონები დროით; სექციის ჯამი: შემთხვევების
    //რაოდენობა და წილი რეპორტის ყველა შემთხვევიდან (Access-ის Count(*)/[AccessTotalsID], ფორმატი Percent);
    //ბოლოს ყველა შემთხვევა
    public static ReportTable TeacherMissAndSubstitutes(IEnumerable<PeriodLessonRow> lessons)
    {
        List<PeriodLessonRow> cases =
        [
            .. lessons.Where(l =>
                l.LessonStatusId == ReportLessonStatuses.Cancelled || l.SubstituteTeacher is not null)
        ];
        List<ReportColumnResponse> columns =
        [
            DateTimeColumn("lessonDt", "თარიღი"), Text("substituteTeacher", "ჩამნაცვლებელი"),
            Text("lessonStatus", "სტატუსი")
        ];
        //მასწავლებლის კონტრაქტის ნომერი უნიკალურია, ამიტომ "გვარი სახელი / ნომერი" მასწავლებელს ცალსახად ალაგებს
        List<ReportSectionResponse> sections =
        [
            .. cases.GroupBy(l => l.TeacherContractId).Select(teacherCases => teacherCases.ToList())
                .OrderBy(teacherCases => teacherCases[0].Teacher.NameWithNumber, StringComparer.Ordinal)
                .Select(teacherCases => new ReportSectionResponse(
                    $"{TeacherCaption}: {teacherCases[0].Teacher.NameWithNumber}", [
                        .. teacherCases.OrderBy(l => l.LessonDt).ThenBy(l => l.LessonId).Select(l =>
                            new List<object?> { l.LessonDt, l.SubstituteTeacher?.NameWithNumber, l.LessonStatusName })
                    ], [CasesCaption, teacherCases.Count, Percent(teacherCases.Count, cases.Count)]))
        ];
        return new ReportTable(columns, sections, [ReportFooters.Count(columns, cases.Count)]);
    }

    //გასაუქმებელი და არასწორად გაუქმებული გაკვეთილები: Access-ის რეპორტების სვეტები და რიგი (დრო)
    private static ReportTable LessonsList(IEnumerable<PeriodLessonRow> lessons)
    {
        return ReportTable.Flat([
            DateTimeColumn("lessonDt", "თარიღი და დრო"), Text("groupCode", "ჯგუფი"), Text("teacher", "მასწავლებელი")
        ], [
            .. lessons.OrderBy(l => l.LessonDt).ThenBy(l => l.GroupCode, StringComparer.Ordinal).ThenBy(l => l.LessonId)
                .Select(l => new List<object?> { l.LessonDt, l.GroupCode, l.Teacher.NameWithNumber })
        ]);
    }

    //Access-ის Percent ფორმატი: ორი ათწილადი და "%"
    private static string Percent(int count, int total)
    {
        return Math.Round(count * 100m / total, 2, MidpointRounding.AwayFromZero)
            .ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }

    private static ReportColumnResponse DateTimeColumn(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.DateTime);
    }
}
