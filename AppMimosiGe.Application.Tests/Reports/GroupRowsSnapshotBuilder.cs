using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Tests.Reports;

//group rows for the check report tests: schemes 1 "S1", 2 "S2", 3 "S3", academic years ending on 2027-09-01;
//synthetic people only, never real names
internal sealed class GroupRowsSnapshotBuilder
{
    private readonly List<CheckDayTimeRow> _dayTimes = [];
    private readonly List<CheckGroup> _groups = [];
    private readonly Dictionary<int, string> _schemeNames = new() { [1] = "S1", [2] = "S2", [3] = "S3" };
    private readonly Dictionary<int, SchedulePerson> _studentNames = [];
    private readonly List<CheckStudentRow> _students = [];
    private readonly Dictionary<int, CheckTeacherContract> _teacherContracts = [];
    private readonly List<CheckTeacherRow> _teachers = [];
    private DateTime? _maxFinishDate = Day(2027, 9, 1);

    public static DateTime Day(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    public GroupRowsSnapshotBuilder Group(int groupId, string groupCode, int courseId = 1, string courseName = "Math",
        DateTime? voidDate = null)
    {
        _groups.Add(new CheckGroup(groupId, groupCode, courseId, courseName, voidDate));
        return this;
    }

    public GroupRowsSnapshotBuilder Student(int gbsId, int groupId, int studentContractId, DateTime startDate,
        DateTime? endDate, decimal fourWeekFee = 48m, decimal oneHourFee = 6m, float hoursCoefficient = 1f,
        float fourWeekHours = 8f)
    {
        _students.Add(new CheckStudentRow(gbsId, groupId, studentContractId, startDate, endDate, fourWeekHours,
            fourWeekFee, oneHourFee, hoursCoefficient));
        _studentNames.TryAdd(studentContractId,
            new SchedulePerson($"SLast{studentContractId}", $"SFirst{studentContractId}",
                $"6.{studentContractId:000}"));
        return this;
    }

    public GroupRowsSnapshotBuilder Teacher(int gbtId, int groupId, int teacherContractId, DateTime startDate,
        DateTime? endDate, int salarySchemaId = 1)
    {
        _teachers.Add(new CheckTeacherRow(gbtId, groupId, teacherContractId, salarySchemaId, startDate, endDate));
        _teacherContracts.TryAdd(teacherContractId,
            new CheckTeacherContract(
                new SchedulePerson($"TLast{teacherContractId}", $"TFirst{teacherContractId}", $"T{teacherContractId}"),
                1));
        return this;
    }

    public GroupRowsSnapshotBuilder DayTime(int gdtpId, int groupId, DateTime startDate, DateTime? endDate,
        float hoursCount = 1.5f)
    {
        _dayTimes.Add(new CheckDayTimeRow(gdtpId, groupId, startDate, endDate, hoursCount));
        return this;
    }

    public GroupRowsSnapshotBuilder StudentName(int studentContractId, string lastName, string firstName,
        string contractNumber)
    {
        _studentNames[studentContractId] = new SchedulePerson(lastName, firstName, contractNumber);
        return this;
    }

    //a teacher contract: name and the main salary scheme (SalarySchemaByHours)
    public GroupRowsSnapshotBuilder TeacherContract(int teacherContractId, string lastName, string firstName,
        string contractNumber, int? mainSchemeId)
    {
        _teacherContracts[teacherContractId] =
            new CheckTeacherContract(new SchedulePerson(lastName, firstName, contractNumber), mainSchemeId);
        return this;
    }

    public GroupRowsSnapshotBuilder MaxFinishDate(DateTime? maxFinishDate)
    {
        _maxFinishDate = maxFinishDate;
        return this;
    }

    public GroupRowsSnapshot Build()
    {
        return new GroupRowsSnapshot(_groups, _students, _teachers, _dayTimes, _studentNames, _teacherContracts,
            _schemeNames, _maxFinishDate);
    }
}
