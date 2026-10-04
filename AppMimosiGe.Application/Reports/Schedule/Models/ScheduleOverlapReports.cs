using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     განრიგის გადაფარვები: ოთახების (r06), მასწავლებლების (r18) და მოსწავლეების (r19). Access ერთსა და იმავე დღეს
///     ერთსა და იმავე დაწყების დროს ითვლიდა გადაფარვად; აქ (მომხმარებლის არჩევანი, D118) გადაფარვა დროის შუალედებისაა:
///     ერთი ოთახის, მასწავლებლის ან მოსწავლის კონტრაქტის ორი გაკვეთილი კვირის ერთსა და იმავე დღეს, რომელთა
///     [დაწყება, დაწყება + საათები) იკვეთება. სტრიქონი = გადაფარვაში მონაწილე გაკვეთილი (განრიგის სტრიქონი), ერთხელ
/// </summary>
public static class ScheduleOverlapReports
{
    //r06RoomOver (vR06Base1, vR06RoomOver): რიგი Access-ის რეპორტისაა: ოთახი, კვირის დღე, დრო; შემდეგ ჯგუფი
    public static ReportTable RoomOverlaps(ScheduleSnapshot schedule)
    {
        return Build(schedule, new ReportColumnResponse("roomName", "ოთახი", ReportColumnTypes.Text),
            schedule.Lessons.Select(lesson =>
                new ScheduleOccupancy(lesson.RoomId, schedule.RoomNames[lesson.RoomId], lesson)));
    }

    //r18TeacherOver (vR18Base1, vR18TeacherOver): მასწავლებლის კონტრაქტი, "გვარი სახელი / ნომერი". Access-ის
    //vR18Base1 GroupsByTeachers.StartDate-ს არ ამოწმებდა (Q9ბ); აქ ყველა სტრიქონი თარიღისთვის მოქმედია (D116)
    public static ReportTable TeacherOverlaps(ScheduleSnapshot schedule)
    {
        return Build(schedule, new ReportColumnResponse("teacher", "მასწავლებელი", ReportColumnTypes.Text),
            from lesson in schedule.Lessons
            join teacher in schedule.Teachers on lesson.GroupId equals teacher.GroupId
            select new ScheduleOccupancy(teacher.TeacherContractId,
                schedule.TeacherNames[teacher.TeacherContractId].NameWithNumber, lesson));
    }

    //r19StudentOver (vR19Base1, vR19StudentOver): მოსწავლის კონტრაქტი, "გვარი სახელი / ნომერი"
    public static ReportTable StudentOverlaps(ScheduleSnapshot schedule)
    {
        return Build(schedule, new ReportColumnResponse("student", "მოსწავლე", ReportColumnTypes.Text),
            from lesson in schedule.Lessons
            join student in schedule.Students on lesson.GroupId equals student.GroupId
            select new ScheduleOccupancy(student.StudentContractId,
                schedule.StudentNames[student.StudentContractId].NameWithNumber, lesson));
    }

    //ყოველი გაკვეთილი, რომელსაც კვირის იმავე დღეს იმავე owner-ის სხვა გაკვეთილი (სხვა განრიგის სტრიქონი) ფარავს.
    //ერთი კონტრაქტის ორი სტრიქონი ერთ ჯგუფში ერთი გაკვეთილია: ასეთი ორმაგი რეგისტრაცია r20-ისა და r21-ის საქმეა
    public static List<ScheduleOccupancy> FindOverlapping(IEnumerable<ScheduleOccupancy> occupancies)
    {
        List<ScheduleOccupancy> overlapping = [];
        foreach (IGrouping<(int OwnerId, int WeekDayId), ScheduleOccupancy> day in occupancies
                     .DistinctBy(o => (o.OwnerId, o.Lesson.GdtpId)).GroupBy(o => (o.OwnerId, o.Lesson.WeekDayId)))
        {
            List<ScheduleOccupancy> lessons = [.. day];
            overlapping.AddRange(lessons.Where(item => lessons.Any(other =>
                other.Lesson.GdtpId != item.Lesson.GdtpId && other.Lesson.Start < item.Lesson.End &&
                item.Lesson.Start < other.Lesson.End)));
        }

        return overlapping;
    }

    private static ReportTable Build(ScheduleSnapshot schedule, ReportColumnResponse ownerColumn,
        IEnumerable<ScheduleOccupancy> occupancies)
    {
        Dictionary<int, string> groupCodes = schedule.Groups.ToDictionary(g => g.GroupId, g => g.GroupCode);
        Dictionary<int, int> dayOrder = schedule.WeekDays.Select((day, index) => (day.WeekDayId, index))
            .ToDictionary(d => d.WeekDayId, d => d.index);
        Dictionary<int, string> dayNames = schedule.WeekDays.ToDictionary(d => d.WeekDayId, d => d.ShortName);

        return ReportTable.Flat([
            ownerColumn, new ReportColumnResponse("weekDay", "კვირის დღე", ReportColumnTypes.Text),
            new ReportColumnResponse("startTime", "დრო", ReportColumnTypes.Time),
            new ReportColumnResponse("endTime", "დასრულება", ReportColumnTypes.Time),
            new ReportColumnResponse("groupCode", "ჯგუფი", ReportColumnTypes.Text)
        ], [
            .. FindOverlapping(occupancies).OrderBy(o => o.OwnerName, StringComparer.Ordinal).ThenBy(o => o.OwnerId)
                .ThenBy(o => dayOrder[o.Lesson.WeekDayId]).ThenBy(o => o.Lesson.Start)
                .ThenBy(o => groupCodes[o.Lesson.GroupId], StringComparer.Ordinal).ThenBy(o => o.Lesson.GdtpId)
                .Select(o => new List<object?>
                {
                    o.OwnerName,
                    dayNames[o.Lesson.WeekDayId],
                    o.Lesson.StartTime,
                    o.Lesson.EndTime,
                    groupCodes[o.Lesson.GroupId]
                })
        ]);
    }
}
