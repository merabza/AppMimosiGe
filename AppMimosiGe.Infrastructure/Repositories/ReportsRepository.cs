using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class ReportsRepository(IMimosiGeDbContext context) : IReportsRepository
{
    //აქტიური ჯგუფები (ActiveGroupsForReports) და მათი სტრიქონები, რომლებიც date დღეს მოქმედებს; სახელები მხოლოდ
    //ამ სტრიქონების კონტრაქტებისა
    public async Task<ScheduleSnapshot> GetSchedule(DateTime date, CancellationToken cancellationToken = default)
    {
        DateTime dayAfter = ActiveGroupsForReports.DayAfter(date);
        IQueryable<int> activeGroupIds = ActiveGroupsForReports.Query(context, date).Select(g => g.GrpId);

        List<ScheduleGroup> groups = await ActiveGroupsForReports.Query(context, date).AsNoTracking()
            .Select(g => new ScheduleGroup(g.GrpId, g.GroupCode, g.CourseId, g.Course.CourseName))
            .ToListAsync(cancellationToken);
        List<ScheduleStudentRow> students = await context.GroupsByStudents.AsNoTracking()
            .Where(s => activeGroupIds.Contains(s.GroupId) && s.StartDate < dayAfter &&
                        (s.EndDate == null || s.EndDate >= dayAfter))
            .Select(s => new ScheduleStudentRow(s.GbsId, s.GroupId, s.StudentContractId))
            .ToListAsync(cancellationToken);
        List<ScheduleTeacherRow> teachers = await context.GroupsByTeachers.AsNoTracking()
            .Where(t => activeGroupIds.Contains(t.GroupId) && t.StartDate < dayAfter &&
                        (t.EndDate == null || t.EndDate >= dayAfter))
            .Select(t => new ScheduleTeacherRow(t.Id, t.GroupId, t.TeacherContractId)).ToListAsync(cancellationToken);
        List<ScheduleLessonRow> lessons = await context.GroupDayTimePlaces.AsNoTracking()
            .Where(d => activeGroupIds.Contains(d.GroupId) && d.StartDate < dayAfter &&
                        (d.EndDate == null || d.EndDate >= dayAfter)).Select(d =>
                new ScheduleLessonRow(d.GdtpId, d.GroupId, d.WeekDayId, d.LessonStartTime.LstTime, d.HoursCount,
                    d.RoomId)).ToListAsync(cancellationToken);

        Dictionary<int, string> roomNames = await context.Rooms.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.RoomName, cancellationToken);
        //crosstab-ის სვეტების ფიქსირებული რიგი: კვირის დღის ნომრით (1 = ორშაბათი)
        List<ScheduleWeekDay> weekDays = await context.WeekDays.AsNoTracking().OrderBy(w => w.WeekDayNumber)
            .ThenBy(w => w.Id).Select(w => new ScheduleWeekDay(w.Id, w.ShortName)).ToListAsync(cancellationToken);

        List<int> teacherContractIds = [.. teachers.Select(t => t.TeacherContractId).Distinct()];
        Dictionary<int, SchedulePerson> teacherNames = await context.TeacherContracts.AsNoTracking()
            .Where(tc => teacherContractIds.Contains(tc.Id))
            .Select(tc => new KeyValuePair<int, SchedulePerson>(tc.Id,
                new SchedulePerson(tc.TeacherHuman.LastName, tc.TeacherHuman.FirstName, tc.ContractNumber)))
            .ToDictionaryAsync(p => p.Key, p => p.Value, cancellationToken);
        List<int> studentContractIds = [.. students.Select(s => s.StudentContractId).Distinct()];
        Dictionary<int, SchedulePerson> studentNames = await context.StudentContracts.AsNoTracking()
            .Where(sc => studentContractIds.Contains(sc.ScId))
            .Select(sc => new KeyValuePair<int, SchedulePerson>(sc.ScId,
                new SchedulePerson(sc.StudentHuman.LastName, sc.StudentHuman.FirstName, sc.ContractNumber)))
            .ToDictionaryAsync(p => p.Key, p => p.Value, cancellationToken);

        return new ScheduleSnapshot(groups, students, teachers, lessons, roomNames, weekDays, teacherNames,
            studentNames);
    }

    //Access-ის FrmMain-ის combo-ები: მასწავლებლები და მოსწავლეები "გვარი სახელი / ნომერი"-თ, საგნები სახელით.
    //მასწავლებლის ნომერი უნიკალურია; მოსწავლისა მხოლოდ წლის ფარგლებში, ამიტომ ერთნაირ სახელებს ID ალაგებს
    public async Task<ReportLookupsResponse> GetLookups(CancellationToken cancellationToken = default)
    {
        List<LookupItemResponse> teachers = await context.TeacherContracts.AsNoTracking()
            .Select(x => new
            {
                x.Id, Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).Select(x => new LookupItemResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
        List<LookupItemResponse> courses = await context.Courses.AsNoTracking().OrderBy(x => x.CourseName)
            .ThenBy(x => x.CrsId).Select(x => new LookupItemResponse(x.CrsId, x.CourseName))
            .ToListAsync(cancellationToken);
        List<LookupItemResponse> students = await context.StudentContracts.AsNoTracking()
            .Select(x => new
            {
                x.ScId, Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).ThenBy(x => x.ScId).Select(x => new LookupItemResponse(x.ScId, x.Name))
            .ToListAsync(cancellationToken);
        return new ReportLookupsResponse(teachers, courses, students);
    }
}
