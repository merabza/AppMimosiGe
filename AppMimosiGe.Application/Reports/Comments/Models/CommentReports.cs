using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Comments.Models;

/// <summary>
///     კომენტარების რეპორტი: დღის კომენტარები თვისთვის (r01)
/// </summary>
public static class CommentReports
{
    private const string DateFormat = "dd.MM.yyyy";
    private const string DateTimeFormat = "dd.MM.yyyy HH:mm";

    //r01Comments (vR01CommentsQuery): თვის გაკვეთილები მოსწავლეებით (მოსწავლის გარეშე გაკვეთილი არ ჩანს, Access-ის
    //INNER JOIN). სექცია = გაკვეთილი (Access-ის ჯგუფისა და გაკვეთილის სათაურები): ჯგუფი, საგანი, მასწავლებელი, დრო,
    //აღდგენის თარიღი და შემცვლელი მასწავლებელი; სტრიქონები: მოსწავლეები ნომრით (თითო გაკვეთილში თავიდან, Access-ის
    //RunningSum) და კომენტარებით. Access-ში კომენტარის, აღდგენისა და შემცვლელის ადგილი ცარიელი იყო ხელით
    //შესავსებად; ახლა შეტანილი მონაცემი ჩანს, შეუვსებელი კი ცარიელია. რიგი: ჯგუფი, დრო; მოსწავლის სახელი
    public static ReportTable DailyComments(IEnumerable<CommentLesson> lessons)
    {
        return new ReportTable(
        [
            new ReportColumnResponse("number", "№", ReportColumnTypes.WholeNumber),
            new ReportColumnResponse("student", "მოსწავლე", ReportColumnTypes.Text),
            new ReportColumnResponse("teacherComment", "მასწავლებლის კომენტარი", ReportColumnTypes.Text),
            new ReportColumnResponse("studentComment", "მოსწავლის კომენტარი", ReportColumnTypes.Text)
        ], [
            .. lessons.OrderBy(l => l.GroupCode, StringComparer.Ordinal).ThenBy(l => l.LessonDt)
                .ThenBy(l => l.LessonId).Select(l => new ReportSectionResponse(Header(l), [
                    .. l.Students.Select(s => (Name: s.Student.NameWithNumber, Row: s))
                        .OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.Row.StudentContractId)
                        .Select((s, index) => new List<object?>
                        {
                            index + 1, s.Name, s.Row.TeacherComment, s.Row.StudentComment
                        })
                ], null))
        ], []);
    }

    //"ჯგუფი: … · საგანი: … · მასწავლებელი: … · თარიღი: … · აღდგენის თარიღი: … · შემცვლელი მასწავლებელი: …";
    //შეუვსებელი აღდგენის თარიღი და შემცვლელი ცარიელია (ხელით შესავსებად, როგორც Access-ში)
    private static string Header(CommentLesson lesson)
    {
        return $"ჯგუფი: {lesson.GroupCode} · საგანი: {lesson.CourseName} · მასწავლებელი: " +
               $"{lesson.Teacher.NameWithNumber} · თარიღი: " +
               $"{lesson.LessonDt.ToString(DateTimeFormat, CultureInfo.InvariantCulture)} · აღდგენის თარიღი: " +
               $"{lesson.RecoverDate?.ToString(DateFormat, CultureInfo.InvariantCulture)} · შემცვლელი მასწავლებელი: " +
               lesson.Substitute?.NameWithNumber;
    }
}
