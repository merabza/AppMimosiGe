using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Tests.Reports;

//active groups for the group report tests: course 1 "Math", size 2 "Four" (4 places), status 1 "S1" unless given;
//rows start on 2026-09-01 unless given; the rows' ids count up unless given. Synthetic people only, never real names
internal sealed class GroupsSnapshotBuilder
{
    private readonly List<ReportGroup> _groups = [];
    private readonly Dictionary<int, SchedulePerson> _studentNames = [];
    private readonly List<ReportGroupStudent> _students = [];
    private readonly Dictionary<int, SchedulePerson> _teacherNames = [];
    private readonly List<ReportGroupTeacher> _teachers = [];
    private int _nextRowId = 1;

    public static DateTime Day(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    public GroupsSnapshotBuilder Group(int groupId, string groupCode, int courseId = 1, string courseName = "Math",
        int sizeId = 2, string sizeName = "Four", int size = 4, int statusId = 1, string statusName = "S1")
    {
        _groups.Add(new ReportGroup(groupId, groupCode, courseId, courseName, sizeId, sizeName, size, statusId,
            statusName));
        return this;
    }

    //one row per student contract
    public GroupsSnapshotBuilder Students(int groupId, params int[] studentContractIds)
    {
        foreach (int studentContractId in studentContractIds)
        {
            Student(groupId, studentContractId);
        }

        return this;
    }

    public GroupsSnapshotBuilder Student(int groupId, int studentContractId, float fourWeekHours = 8f,
        decimal fourWeekFee = 48m, float hoursCoefficient = 1f, DateTime? startDate = null, int? rowId = null)
    {
        _students.Add(new ReportGroupStudent(rowId ?? _nextRowId++, groupId, studentContractId, fourWeekHours,
            fourWeekFee, hoursCoefficient, startDate ?? Day(2026, 9, 1)));
        _studentNames.TryAdd(studentContractId,
            new SchedulePerson($"SLast{studentContractId}", $"SFirst{studentContractId}",
                $"6.{studentContractId:000}"));
        return this;
    }

    public GroupsSnapshotBuilder Teacher(int groupId, int teacherContractId, string schemeName = "Scheme",
        DateTime? startDate = null, int? rowId = null)
    {
        _teachers.Add(new ReportGroupTeacher(rowId ?? _nextRowId++, groupId, teacherContractId, schemeName,
            startDate ?? Day(2026, 9, 1)));
        _teacherNames.TryAdd(teacherContractId,
            new SchedulePerson($"TLast{teacherContractId}", $"TFirst{teacherContractId}", $"T{teacherContractId}"));
        return this;
    }

    public GroupsSnapshotBuilder StudentName(int studentContractId, string lastName, string firstName,
        string contractNumber)
    {
        _studentNames[studentContractId] = new SchedulePerson(lastName, firstName, contractNumber);
        return this;
    }

    public GroupsSnapshotBuilder TeacherName(int teacherContractId, string lastName, string firstName,
        string contractNumber)
    {
        _teacherNames[teacherContractId] = new SchedulePerson(lastName, firstName, contractNumber);
        return this;
    }

    public GroupsSnapshot Build()
    {
        return new GroupsSnapshot(_groups, _students, _teachers, _studentNames, _teacherNames);
    }
}
