using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Tests.Reports;

//a schedule for the report logic tests: seven week days "1-ორ" … "7-კვ" (ids 1–7) and rooms 1 "R1", 2 "R2";
//synthetic people only, never real names
internal sealed class ScheduleSnapshotBuilder
{
    private readonly List<ScheduleGroup> _groups = [];
    private readonly List<ScheduleLessonRow> _lessons = [];
    private readonly Dictionary<int, string> _roomNames = new() { [1] = "R1", [2] = "R2" };
    private readonly Dictionary<int, SchedulePerson> _studentNames = [];
    private readonly List<ScheduleStudentRow> _students = [];
    private readonly Dictionary<int, SchedulePerson> _teacherNames = [];
    private readonly List<ScheduleTeacherRow> _teachers = [];

    private List<ScheduleWeekDay> _weekDays =
    [
        new(1, "1-ორ"), new(2, "2-სამ"), new(3, "3-ოთხ"), new(4, "4-ხუთ"), new(5, "5-პარ"), new(6, "6-შაბ"),
        new(7, "7-კვ")
    ];

    public static TimeOnly Time(string value)
    {
        return TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
    }

    public ScheduleSnapshotBuilder Group(int groupId, string groupCode, int courseId = 1, string courseName = "Math")
    {
        _groups.Add(new ScheduleGroup(groupId, groupCode, courseId, courseName));
        return this;
    }

    public ScheduleSnapshotBuilder Lesson(int gdtpId, int groupId, int weekDayId, string startTime,
        float hoursCount = 1.5f, int roomId = 1)
    {
        _lessons.Add(new ScheduleLessonRow(gdtpId, groupId, weekDayId, Time(startTime), hoursCount, roomId));
        return this;
    }

    public ScheduleSnapshotBuilder Teacher(int gbtId, int groupId, int teacherContractId)
    {
        _teachers.Add(new ScheduleTeacherRow(gbtId, groupId, teacherContractId));
        _teacherNames.TryAdd(teacherContractId,
            new SchedulePerson($"TLast{teacherContractId}", $"TFirst{teacherContractId}", $"T{teacherContractId}"));
        return this;
    }

    public ScheduleSnapshotBuilder Student(int gbsId, int groupId, int studentContractId)
    {
        _students.Add(new ScheduleStudentRow(gbsId, groupId, studentContractId));
        _studentNames.TryAdd(studentContractId,
            new SchedulePerson($"SLast{studentContractId}", $"SFirst{studentContractId}",
                $"6.{studentContractId:000}"));
        return this;
    }

    public ScheduleSnapshotBuilder TeacherName(int teacherContractId, string lastName, string firstName,
        string contractNumber)
    {
        _teacherNames[teacherContractId] = new SchedulePerson(lastName, firstName, contractNumber);
        return this;
    }

    public ScheduleSnapshotBuilder StudentName(int studentContractId, string lastName, string firstName,
        string contractNumber)
    {
        _studentNames[studentContractId] = new SchedulePerson(lastName, firstName, contractNumber);
        return this;
    }

    public ScheduleSnapshotBuilder Room(int roomId, string roomName)
    {
        _roomNames[roomId] = roomName;
        return this;
    }

    public ScheduleSnapshotBuilder WeekDays(params ScheduleWeekDay[] weekDays)
    {
        _weekDays = [.. weekDays];
        return this;
    }

    public ScheduleSnapshot Build()
    {
        return new ScheduleSnapshot(_groups, _students, _teachers, _lessons, _roomNames, _weekDays, _teacherNames,
            _studentNames);
    }
}
