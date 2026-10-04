using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     ერთსა და იმავე თარიღზე დაწყებული და დამთავრებული სტრიქონები (Access-ის StartDate = [EndDate]): ასეთი სტრიქონი
///     [StartDate, EndDate) არცერთ დღეს არ მოქმედებს. მოსწავლის (r31), მასწავლებლის (r32) და განრიგის (r33) სტრიქონები.
///     ჯგუფის ფორმა ასეთს ვეღარ შეინახავს (D55), ამიტომ ეს გადმოტანილი მონაცემების შემოწმებაა
/// </summary>
public static class SameStartEndDateReports
{
    //r31StudSameStartEndDate (vR31StudSameStartEndDate). რიგი: მოსწავლე (Access-ის ORDER BY), ჯგუფი, თარიღი
    public static ReportTable StudentSameStartEndDate(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        return Build([Text("groupCode", "ჯგუფი"), Text("student", "მოსწავლე"), .. PeriodColumns()], [
            .. snapshot.Students.Where(s => s.StartDate == s.EndDate)
                .Select(s => (Row: s, Group: groups[s.GroupId], Student: snapshot.StudentNames[s.StudentContractId]))
                .OrderBy(s => s.Student.NameWithNumber, StringComparer.Ordinal)
                .ThenBy(s => s.Group.GroupCode, StringComparer.Ordinal).ThenBy(s => s.Row.StartDate).Select(s =>
                    new List<object?> { s.Group.GroupCode, s.Student.NameWithNumber, s.Row.StartDate, s.Row.EndDate })
        ]);
    }

    //r32TeachSameStartEndDate (vR32TeachSameStartEndDate). რიგი: მასწავლებელი (Access-ის ORDER BY), ჯგუფი, თარიღი
    public static ReportTable TeacherSameStartEndDate(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        return Build([Text("groupCode", "ჯგუფი"), Text("teacher", "მასწავლებელი"), .. PeriodColumns()], [
            .. snapshot.Teachers.Where(t => t.StartDate == t.EndDate)
                .Select(t => (Row: t, Group: groups[t.GroupId],
                    Teacher: snapshot.TeacherContracts[t.TeacherContractId].Person))
                .OrderBy(t => t.Teacher.NameWithNumber, StringComparer.Ordinal)
                .ThenBy(t => t.Group.GroupCode, StringComparer.Ordinal).ThenBy(t => t.Row.StartDate).Select(t =>
                    new List<object?> { t.Group.GroupCode, t.Teacher.NameWithNumber, t.Row.StartDate, t.Row.EndDate })
        ]);
    }

    //r33DTPSameStartEndDate (vR33DTPSameStartEndDate). Access-ის query-ს რიგი არ ჰქონდა: ჯგუფი, თარიღი
    public static ReportTable DayTimeSameStartEndDate(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        return Build([Text("groupCode", "ჯგუფი"), .. PeriodColumns()], [
            .. snapshot.DayTimes.Where(d => d.StartDate == d.EndDate).Select(d => (Row: d, Group: groups[d.GroupId]))
                .OrderBy(d => d.Group.GroupCode, StringComparer.Ordinal).ThenBy(d => d.Row.StartDate)
                .Select(d => new List<object?> { d.Group.GroupCode, d.Row.StartDate, d.Row.EndDate })
        ]);
    }

    //ბოლოს Access-ის რეპორტის ჩანაწერების რაოდენობა
    private static ReportTable Build(List<ReportColumnResponse> columns, List<List<object?>> rows)
    {
        return ReportTable.Flat(columns, rows, [ReportFooters.Count(columns, rows.Count)]);
    }

    private static IEnumerable<ReportColumnResponse> PeriodColumns()
    {
        return
        [
            new ReportColumnResponse("startDate", "დაწყება", ReportColumnTypes.Date),
            new ReportColumnResponse("endDate", "დასრულება", ReportColumnTypes.Date)
        ];
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }
}
