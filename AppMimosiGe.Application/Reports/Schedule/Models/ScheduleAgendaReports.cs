using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     განრიგის ცხრილები (Access-ის crosstab-ები, კვირის დღეები სვეტებად): ოთახების (r03), მოსწავლეების (r04) და
///     მასწავლებლების (r05) ცხრილი და დატვირთვა დღეებისა და საათების მიხედვით (r07). r03–r05-ის უჯრაში დღის
///     გაკვეთილის დაწყების დროა; Access-ის First() აქ MIN-ია: ერთ უჯრაში რამდენიმე დროისას ყველაზე ადრეული ჩანს (D117)
/// </summary>
public static class ScheduleAgendaReports
{
    private const string DaySumsCaption = "დღეების ჯამები:";
    private const string TotalCaption = "სულ:";

    //r03RoomsAgenda (vR03RoomsAgendaBase1–3): სტრიქონი = ოთახი, მასწავლებლის გვარი და სახელი, საგანი, ჯგუფი.
    //რიგი Access-ის crosstab-ისაა (GROUP BY-ის რიგი): ოთახი, გვარი, სახელი, საგანი, ჯგუფი
    public static ReportTable RoomsAgenda(ScheduleSnapshot schedule)
    {
        Dictionary<int, ScheduleGroup> groups = schedule.Groups.ToDictionary(g => g.GroupId);
        List<AgendaCell<RoomAgendaKey>> cells =
        [
            .. LessonTeachers(schedule).Select(lt => new AgendaCell<RoomAgendaKey>(
                new RoomAgendaKey(schedule.RoomNames[lt.Lesson.RoomId], lt.Teacher.LastName, lt.Teacher.FirstName,
                    groups[lt.Lesson.GroupId].CourseName, groups[lt.Lesson.GroupId].GroupCode), lt.Lesson))
        ];

        return ReportTable.Flat([
            Text("roomName", "ოთახი"), Text("lastName", "გვარი"), Text("firstName", "სახელი"),
            Text("courseName", "საგანი"), Text("groupCode", "ჯგუფი"),
            .. WeekDayPivot.WeekDayColumns(schedule.WeekDays, ReportColumnTypes.Time)
        ], [
            .. Pivot(cells, schedule).OrderBy(r => r.Key.RoomName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.LastName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.FirstName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.CourseName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.GroupCode, StringComparer.Ordinal).Select(r =>
                    Row([r.Key.RoomName, r.Key.LastName, r.Key.FirstName, r.Key.CourseName, r.Key.GroupCode], r.Days))
        ]);
    }

    //r04StudentsAgenda (vR04StudentsAgendaBase1–3): სტრიქონი = მოსწავლე ("გვარი სახელი"), ჯგუფი, საგანი,
    //მასწავლებელი ("გვარი სახელი"). რიგი: მოსწავლე, ჯგუფი, საგანი, მასწავლებელი
    public static ReportTable StudentsAgenda(ScheduleSnapshot schedule)
    {
        Dictionary<int, ScheduleGroup> groups = schedule.Groups.ToDictionary(g => g.GroupId);
        List<AgendaCell<StudentAgendaKey>> cells =
        [
            .. from lessonTeacher in LessonTeachers(schedule)
            join student in schedule.Students on lessonTeacher.Lesson.GroupId equals student.GroupId
            select new AgendaCell<StudentAgendaKey>(
                new StudentAgendaKey(schedule.StudentNames[student.StudentContractId].FullName,
                    groups[student.GroupId].GroupCode, groups[student.GroupId].CourseName,
                    lessonTeacher.Teacher.FullName), lessonTeacher.Lesson)
        ];

        return ReportTable.Flat([
            Text("studentName", "მოსწავლე"), Text("courseName", "საგანი"), Text("groupCode", "ჯგუფი"),
            Text("teacherName", "მასწავლებელი"),
            .. WeekDayPivot.WeekDayColumns(schedule.WeekDays, ReportColumnTypes.Time)
        ], [
            .. Pivot(cells, schedule).OrderBy(r => r.Key.StudentName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.GroupCode, StringComparer.Ordinal)
                .ThenBy(r => r.Key.CourseName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.TeacherName, StringComparer.Ordinal).Select(r =>
                    Row([r.Key.StudentName, r.Key.CourseName, r.Key.GroupCode, r.Key.TeacherName], r.Days))
        ]);
    }

    //r05TeachersAgenda (vR05TeachersAgendaBase1–3): სტრიქონი = მასწავლებლის გვარი და სახელი, საგანი, ჯგუფი, ოთახი.
    //რიგი: გვარი, სახელი, საგანი, ჯგუფი, ოთახი
    public static ReportTable TeachersAgenda(ScheduleSnapshot schedule)
    {
        Dictionary<int, ScheduleGroup> groups = schedule.Groups.ToDictionary(g => g.GroupId);
        List<AgendaCell<TeacherAgendaKey>> cells =
        [
            .. LessonTeachers(schedule).Select(lt => new AgendaCell<TeacherAgendaKey>(
                new TeacherAgendaKey(lt.Teacher.LastName, lt.Teacher.FirstName, groups[lt.Lesson.GroupId].CourseName,
                    groups[lt.Lesson.GroupId].GroupCode, schedule.RoomNames[lt.Lesson.RoomId]), lt.Lesson))
        ];

        return ReportTable.Flat([
            Text("lastName", "გვარი"), Text("firstName", "სახელი"), Text("courseName", "საგანი"),
            Text("groupCode", "ჯგუფი"), Text("roomName", "ოთახი"),
            .. WeekDayPivot.WeekDayColumns(schedule.WeekDays, ReportColumnTypes.Time)
        ], [
            .. Pivot(cells, schedule).OrderBy(r => r.Key.LastName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.FirstName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.CourseName, StringComparer.Ordinal)
                .ThenBy(r => r.Key.GroupCode, StringComparer.Ordinal)
                .ThenBy(r => r.Key.RoomName, StringComparer.Ordinal).Select(r =>
                    Row([r.Key.LastName, r.Key.FirstName, r.Key.CourseName, r.Key.GroupCode, r.Key.RoomName], r.Days))
        ]);
    }

    //r07UsedDayTimes (vR07UsedDayTimesBase1–3). Access-ის სტრიქონი დაწყების დრო იყო; აქ (მომხმარებლის მოთხოვნა, D176)
    //სტრიქონი = დროის მონაკვეთი: ყველა გაკვეთილის დაწყება და დასრულება (დაწყება + საათები) დღეს მონაკვეთებად ყოფს,
    //ჩანს მხოლოდ დაკავებული მონაკვეთები. უჯრა = ამ დღეს ამ მონაკვეთს ფარავი გაკვეთილების (განრიგის სტრიქონების)
    //რაოდენობა, ასე რომ ერთი გაკვეთილი რამდენიმე მონაკვეთშია; ცარიელი = 0. ქვემოთ Access-ის რეპორტის ჯამები: თითო
    //დღის გაკვეთილები და სულ (თითო გაკვეთილი ერთხელ)
    public static ReportTable UsedDayTimes(ScheduleSnapshot schedule)
    {
        List<TimeSpan> bounds =
        [
            .. schedule.Lessons.SelectMany(l => new[] { l.Start, l.End }).Distinct().Order()
        ];
        List<PivotRow<TimeSegment>> rows = WeekDayPivot.Pivot(
            from lesson in schedule.Lessons
            from segment in bounds.Zip(bounds.Skip(1), (start, end) => new TimeSegment(start, end))
            where lesson.Start < segment.End && segment.Start < lesson.End
            select (Lesson: lesson, Segment: segment), x => x.Segment, x => x.Lesson.WeekDayId,
            lessons => lessons.Count, schedule.WeekDays);
        List<object?> daySums =
        [
            .. schedule.WeekDays.Select(day => (object?)schedule.Lessons.Count(l => l.WeekDayId == day.WeekDayId))
        ];

        return ReportTable.Flat([
            new ReportColumnResponse("startTime", "დრო", ReportColumnTypes.Time),
            new ReportColumnResponse("endTime", "დასრულება", ReportColumnTypes.Time),
            .. WeekDayPivot.WeekDayColumns(schedule.WeekDays, ReportColumnTypes.WholeNumber)
        ], [
            .. rows.OrderBy(r => r.Key.Start).Select(r => Row([TimeOfDay(r.Key.Start), TimeOfDay(r.Key.End)], r.Days))
        ], [
            Row([DaySumsCaption, null], daySums),
            Row([TotalCaption, null, schedule.Lessons.Count], schedule.WeekDays.Skip(1).Select(_ => (object?)null))
        ]);
    }

    //დრო დღის დასაწყისიდან (შუაღამის შემდეგ დასრულება ისე ჩანს, როგორც ScheduleLessonRow.EndTime)
    private static TimeOnly TimeOfDay(TimeSpan time)
    {
        return TimeOnly.MinValue.Add(time);
    }

    //განრიგის სტრიქონი × ამავე ჯგუფის მასწავლებლის სტრიქონი (Access-ის INNER JOIN GroupsByTeachers)
    private static IEnumerable<(ScheduleLessonRow Lesson, SchedulePerson Teacher)> LessonTeachers(
        ScheduleSnapshot schedule)
    {
        return from lesson in schedule.Lessons
            join teacher in schedule.Teachers on lesson.GroupId equals teacher.GroupId
            select (lesson, schedule.TeacherNames[teacher.TeacherContractId]);
    }

    private static List<PivotRow<TKey>> Pivot<TKey>(List<AgendaCell<TKey>> cells, ScheduleSnapshot schedule)
        where TKey : notnull
    {
        return WeekDayPivot.Pivot(cells, c => c.Key, c => c.Lesson.WeekDayId, cell => cell.Min(c => c.Lesson.StartTime),
            schedule.WeekDays);
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }

    private static List<object?> Row(IEnumerable<object?> first, IEnumerable<object?> rest)
    {
        return [.. first, .. rest];
    }

    private sealed record AgendaCell<TKey>(TKey Key, ScheduleLessonRow Lesson);

    //r07-ის დროის მონაკვეთი [Start, End)
    private sealed record TimeSegment(TimeSpan Start, TimeSpan End);

    private sealed record RoomAgendaKey(
        string RoomName,
        string LastName,
        string FirstName,
        string CourseName,
        string GroupCode);

    private sealed record StudentAgendaKey(string StudentName, string GroupCode, string CourseName, string TeacherName);

    private sealed record TeacherAgendaKey(
        string LastName,
        string FirstName,
        string CourseName,
        string GroupCode,
        string RoomName);
}
