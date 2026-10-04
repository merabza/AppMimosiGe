using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     ჯგუფის სტრიქონების შეუსაბამობები: მასწავლებლის ხელფასის სქემა (r29) და მოსწავლის 4 კვირის გადასახადი (r30)
/// </summary>
public static class MismatchReports
{
    //Access-ის Abs(…) > 0.01
    private const decimal FeeTolerance = 0.01m;
    private const int WeeksInFourWeeks = 4;
    private const int MoneyDecimals = 4;

    //r29TeacherMissSalary (vR29TeacherMissSalary): ჯგუფის მასწავლებლის სტრიქონის სქემა კონტრაქტის ძირითადი სქემისგან
    //(SalarySchemaByHours) განსხვავდება; ძირითადი სქემის გარეშე კონტრაქტი არ ჩანს (Access-ის INNER JOIN). სტრიქონის
    //თარიღები არ მოწმდება. რიგი: მასწავლებელი (Access-ის ORDER BY და რეპორტის დალაგება), ჯგუფი, ჯგუფის სქემა
    public static ReportTable TeacherSchemeMismatches(GroupRowsSnapshot snapshot)
    {
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        return ReportTable.Flat([
            Text("teacher", "მასწავლებელი"), Text("groupCode", "ჯგუფი"), Text("contractScheme", "კონტრაქტის სქემა"),
            Text("groupScheme", "ჯგუფის სქემა")
        ], [
            .. snapshot.Teachers.Select(t => (Row: t, Contract: snapshot.TeacherContracts[t.TeacherContractId]))
                .Where(t => t.Contract.SalarySchemaByHoursId is { } mainSchemeId && mainSchemeId != t.Row.SalarySchemaId)
                .Select(t => (Teacher: t.Contract.Person.NameWithNumber, groups[t.Row.GroupId].GroupCode,
                    ContractScheme: snapshot.SalarySchemeNames[t.Contract.SalarySchemaByHoursId!.Value],
                    GroupScheme: snapshot.SalarySchemeNames[t.Row.SalarySchemaId]))
                .OrderBy(t => t.Teacher, StringComparer.Ordinal).ThenBy(t => t.GroupCode, StringComparer.Ordinal)
                .ThenBy(t => t.GroupScheme, StringComparer.Ordinal).Select(t =>
                    new List<object?> { t.Teacher, t.GroupCode, t.ContractScheme, t.GroupScheme })
        ]);
    }

    //r30StudentMissFees (vStudentGroupDates, vStudenGroupWeekHoursCounts, vR30StudentMissFees): ჯგუფის მოსწავლის
    //(ჯგუფი, კონტრაქტი) ყოველი ცვლილების თარიღისთვის, რომელსაც მოქმედი განრიგი აქვს: ამ დღეს მოქმედი მოსწავლის
    //სტრიქონის 4 კვირის გადასახადი უნდა იყოს საათის ღირებულება × კოეფიციენტი × განრიგის კვირის საათები × 4.
    //0.01-ზე მეტი სხვაობა ჩანს ამ დღეს მოქმედ ყოველ მასწავლებლის სტრიქონთან ერთად (Access-ის join, D124).
    //ცარიელი დასრულება Access-ივით სასწავლო წლების ბოლო დასასრულია (vMaxDate). ბოლოს Access-ის რეპორტის ჯამი
    //Sum([FourWeekFee])
    public static ReportTable StudentFeeMismatches(GroupRowsSnapshot snapshot)
    {
        DateTime? maxFinishDate = snapshot.MaxFinishDate;
        Dictionary<int, CheckGroup> groups = snapshot.Groups.ToDictionary(g => g.GroupId);
        ILookup<int, CheckTeacherRow> teachers = snapshot.Teachers.ToLookup(t => t.GroupId);
        ILookup<int, CheckDayTimeRow> dayTimes = snapshot.DayTimes.ToLookup(d => d.GroupId);
        List<FeeMismatch> mismatches = [];
        foreach (IGrouping<(int GroupId, int StudentContractId), CheckStudentRow> studentRows in snapshot.Students
                     .GroupBy(s => (s.GroupId, s.StudentContractId)))
        {
            CheckGroup studentGroup = groups[studentRows.Key.GroupId];
            foreach (DateTime date in ChangeDates(studentRows, studentGroup, teachers[studentGroup.GroupId],
                         dayTimes[studentGroup.GroupId], maxFinishDate))
            {
                List<CheckDayTimeRow> activeDayTimes =
                    [.. dayTimes[studentGroup.GroupId].Where(d => IsActive(d.StartDate, d.EndDate, date, maxFinishDate))];
                //Access-ის vStudenGroupWeekHoursCounts-ში ასეთ თარიღს სტრიქონი არ აქვს
                if (activeDayTimes.Count == 0)
                {
                    continue;
                }

                decimal weekHours = activeDayTimes.Sum(d => (decimal)d.HoursCount);
                mismatches.AddRange(
                    from student in studentRows.Where(s => IsActive(s.StartDate, s.EndDate, date, maxFinishDate))
                    let mustFee = student.OneHourFee * (decimal)student.HoursCoefficient * weekHours * WeeksInFourWeeks
                    where Math.Abs(mustFee - student.FourWeekFee) > FeeTolerance
                    from teacher in teachers[studentGroup.GroupId]
                        .Where(t => IsActive(t.StartDate, t.EndDate, date, maxFinishDate))
                    select new FeeMismatch(studentGroup, snapshot.StudentNames[student.StudentContractId], student, teacher,
                        date, weekHours, mustFee));
            }
        }

        List<ReportColumnResponse> columns =
        [
            Text("groupCode", "ჯგუფი"), Text("student", "მოსწავლე"), Date("changeDate", "თარიღი"),
            Date("studentStartDate", "მოსწავლის დაწყება"), Date("studentEndDate", "მოსწავლის დასრულება"),
            Date("teacherStartDate", "მასწავლებლის დაწყება"), Date("teacherEndDate", "მასწავლებლის დასრულება"),
            Number("fourWeekHours", "4 კვირაში საათები"), Number("oneHourFee", "საათის ღირებულება"),
            Number("hoursCoefficient", "საათის კოეფიციენტი"), Number("weekHours", "კვირაში საათები"),
            Number("fourWeekFee", "4 კვირაში გადასახადი"), Number("mustFourWeekFee", "უნდა იყოს"),
            Number("missFourWeekFee", "სხვაობა")
        ];
        List<object?> footer = [ReportFooters.TotalCaption, .. Enumerable.Repeat<object?>(null, columns.Count - 1)];
        footer[columns.FindIndex(c => c.Name == "fourWeekFee")] = mismatches.Sum(m => m.Student.FourWeekFee);
        return ReportTable.Flat(columns, [
            .. mismatches.OrderBy(m => m.Group.GroupCode, StringComparer.Ordinal).ThenBy(m => m.Group.GroupId)
                .ThenBy(m => m.StudentName.NameWithNumber, StringComparer.Ordinal).ThenBy(m => m.ChangeDate)
                .ThenBy(m => m.Student.GbsId).ThenBy(m => m.Teacher.GbtId).Select(m => new List<object?>
                {
                    m.Group.GroupCode,
                    m.StudentName.NameWithNumber,
                    m.ChangeDate,
                    m.Student.StartDate,
                    m.Student.EndDate ?? maxFinishDate,
                    m.Teacher.StartDate,
                    m.Teacher.EndDate ?? maxFinishDate,
                    m.Student.FourWeekHours,
                    m.Student.OneHourFee,
                    m.Student.HoursCoefficient,
                    m.WeekHours,
                    m.Student.FourWeekFee,
                    Math.Round(m.MustFee, MoneyDecimals),
                    Math.Round(m.MustFee - m.Student.FourWeekFee, MoneyDecimals)
                })
        ], [footer]);
    }

    //Access-ის vStudentGroupDates (UNION, ანუ განსხვავებული): წყვილის მოსწავლის, ჯგუფის განრიგის და მასწავლებლის
    //სტრიქონების დაწყება და დასრულება და ჯგუფის გაუქმება; ცარიელი დასრულება = vMaxDate
    private static IEnumerable<DateTime> ChangeDates(IEnumerable<CheckStudentRow> studentRows, CheckGroup group,
        IEnumerable<CheckTeacherRow> teachers, IEnumerable<CheckDayTimeRow> dayTimes, DateTime? maxFinishDate)
    {
        IEnumerable<DateTime?> dates =
        [
            .. studentRows.SelectMany(s => new[] { s.StartDate, s.EndDate ?? maxFinishDate }),
            .. dayTimes.SelectMany(d => new[] { d.StartDate, d.EndDate ?? maxFinishDate }),
            .. teachers.SelectMany(t => new[] { t.StartDate, t.EndDate ?? maxFinishDate }),
            group.VoidDate ?? maxFinishDate
        ];
        return dates.OfType<DateTime>().Distinct();
    }

    //Access-ის StartDate <= OpDate And IIf(IsNull(EndDate), vMaxDate, EndDate) > OpDate
    private static bool IsActive(DateTime startDate, DateTime? endDate, DateTime date, DateTime? maxFinishDate)
    {
        return startDate <= date && (endDate ?? maxFinishDate) > date;
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }

    private static ReportColumnResponse Date(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Date);
    }

    private static ReportColumnResponse Number(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Number);
    }

    private sealed record FeeMismatch(
        CheckGroup Group,
        SchedulePerson StudentName,
        CheckStudentRow Student,
        CheckTeacherRow Teacher,
        DateTime ChangeDate,
        decimal WeekHours,
        decimal MustFee);
}
