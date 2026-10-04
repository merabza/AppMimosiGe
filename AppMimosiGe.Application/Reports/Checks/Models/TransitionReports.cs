using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     აცდენილი გადასვლები: სტრიქონის დასრულების შემდეგ იმავე ჯაჭვის შემდეგი სტრიქონი სხვა დღეს იწყება, ანუ შუალედში
///     მოსწავლეს (r26), ჯგუფს მასწავლებელი (r27) ან განრიგი (r28) არ აქვს. შემდეგი სტრიქონის დაწყება Access-ის
///     MinOfStartDate-ია: ჯაჭვის ყველაზე ადრეული დაწყება, რომელიც დასრულების დღეს ან მის შემდეგაა
/// </summary>
public static class TransitionReports
{
    //r26StudMissDate (vR26StudMissDateBase0, vR26StudMissDateBase1, vR26StudMissDate): ჯაჭვი = მოსწავლის კონტრაქტი
    //და საგანი (ნებისმიერი ჯგუფი). რიგი: მოსწავლე (Access-ის ORDER BY)
    public static ReportTable StudentMissedTransitions(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        List<(CheckStudentRow Row, CheckGroup Group)> rows = [.. snapshot.Students.Select(s => (s, groups[s.GroupId]))];
        List<ReportColumnResponse> columns =
        [
            Text("courseName", "საგანი"), Text("student", "მოსწავლე"), .. PeriodColumns()
        ];
        List<List<object?>> result =
        [
            .. rows.Select(r => (r.Row, r.Group, Next: NextStart(r.Row.EndDate,
                    rows.Where(o => o.Row.StudentContractId == r.Row.StudentContractId &&
                                    o.Group.CourseId == r.Group.CourseId).Select(o => o.Row.StartDate))))
                .Where(r => IsMissed(r.Row.EndDate, r.Next))
                .DistinctBy(r => (r.Group.CourseId, r.Row.StudentContractId, r.Row.StartDate, r.Row.EndDate))
                .Select(r => (r.Row, r.Group, r.Next, Student: snapshot.StudentNames[r.Row.StudentContractId]))
                .OrderBy(r => r.Student.NameWithNumber, StringComparer.Ordinal)
                .ThenBy(r => r.Group.CourseName, StringComparer.Ordinal).ThenBy(r => r.Row.StartDate)
                .ThenBy(r => r.Row.EndDate).ThenBy(r => r.Row.StudentContractId).Select(r => new List<object?>
                {
                    r.Group.CourseName, r.Student.NameWithNumber, r.Row.StartDate, r.Row.EndDate, r.Next
                })
        ];
        return ReportTable.Flat(columns, result, [ReportFooters.Count(columns, result.Count)]);
    }

    //r27TeacherMissDate (vR27TeacherMissDateBase1, vR27TeacherMissDate): ჯაჭვი = ჯგუფის ყველა მასწავლებლის
    //სტრიქონი (D123; Access მხოლოდ იმავე მასწავლებლის შემდეგ სტრიქონს ეძებდა). რიგი: მასწავლებელი (Access-ის ORDER BY)
    public static ReportTable TeacherMissedTransitions(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        List<ReportColumnResponse> columns =
        [
            Text("groupCode", "ჯგუფი"), Text("teacher", "მასწავლებელი"), .. PeriodColumns()
        ];
        List<List<object?>> result =
        [
            .. snapshot.Teachers.Select(t => (Row: t, Next: NextStart(t.EndDate,
                    snapshot.Teachers.Where(o => o.GroupId == t.GroupId).Select(o => o.StartDate))))
                .Where(t => IsMissed(t.Row.EndDate, t.Next))
                .DistinctBy(t => (t.Row.GroupId, t.Row.TeacherContractId, t.Row.StartDate, t.Row.EndDate))
                .Select(t => (t.Row, t.Next, Group: groups[t.Row.GroupId],
                    Teacher: snapshot.TeacherContracts[t.Row.TeacherContractId].Person))
                .OrderBy(t => t.Teacher.NameWithNumber, StringComparer.Ordinal)
                .ThenBy(t => t.Group.GroupCode, StringComparer.Ordinal).ThenBy(t => t.Row.GroupId)
                .ThenBy(t => t.Row.StartDate).ThenBy(t => t.Row.EndDate).Select(t => new List<object?>
                {
                    t.Group.GroupCode, t.Teacher.NameWithNumber, t.Row.StartDate, t.Row.EndDate, t.Next
                })
        ];
        return ReportTable.Flat(columns, result, [ReportFooters.Count(columns, result.Count)]);
    }

    //r28DayTimesMissDate (vR28DayTimesMissDateBase1, vR28DayTimesMissDate): ჯაჭვი = ჯგუფის განრიგის ყველა სტრიქონი,
    //კვირის დღის მიუხედავად (Access). Access-ის query-ს რიგი არ ჰქონდა: ჯგუფი, დაწყება, დასრულება
    public static ReportTable DayTimeMissedTransitions(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        List<ReportColumnResponse> columns = [Text("groupCode", "ჯგუფი"), .. PeriodColumns()];
        List<List<object?>> result =
        [
            .. snapshot.DayTimes.Select(d => (Row: d, Next: NextStart(d.EndDate,
                    snapshot.DayTimes.Where(o => o.GroupId == d.GroupId).Select(o => o.StartDate))))
                .Where(d => IsMissed(d.Row.EndDate, d.Next))
                .DistinctBy(d => (d.Row.GroupId, d.Row.StartDate, d.Row.EndDate))
                .Select(d => (d.Row, d.Next, Group: groups[d.Row.GroupId]))
                .OrderBy(d => d.Group.GroupCode, StringComparer.Ordinal).ThenBy(d => d.Row.GroupId)
                .ThenBy(d => d.Row.StartDate).ThenBy(d => d.Row.EndDate).Select(d => new List<object?>
                {
                    d.Group.GroupCode, d.Row.StartDate, d.Row.EndDate, d.Next
                })
        ];
        return ReportTable.Flat(columns, result, [ReportFooters.Count(columns, result.Count)]);
    }

    //Access-ის Base1-ის Min(StartDate): ჯაჭვის სტრიქონები, რომლებიც დასრულების დღეს ან მის შემდეგ იწყება (თავად
    //სტრიქონიც, თუ მისი დაწყება დასრულებაზე ადრე არ არის: Access-ის join-ში EndDate <= StartDate). დასრულების გარეშე
    //სტრიქონს შემდეგი არ აქვს: Null-თან შედარება (Access-შიც) არცერთ დაწყებას არ ტოვებს
    private static DateTime? NextStart(DateTime? endDate, IEnumerable<DateTime> chainStarts)
    {
        return chainStarts.Where(start => start >= endDate).Select(start => (DateTime?)start).Min();
    }

    //Access-ის EndDate <> MinOfStartDate: შემდეგი სტრიქონი არსებობს და დასრულების დღეს არ იწყება
    private static bool IsMissed(DateTime? endDate, DateTime? nextStart)
    {
        return nextStart is not null && nextStart != endDate;
    }

    private static IEnumerable<ReportColumnResponse> PeriodColumns()
    {
        return
        [
            new ReportColumnResponse("startDate", "დაწყება", ReportColumnTypes.Date),
            new ReportColumnResponse("endDate", "დასრულება", ReportColumnTypes.Date),
            new ReportColumnResponse("nextStartDate", "შემდეგის დაწყება", ReportColumnTypes.Date)
        ];
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }
}
