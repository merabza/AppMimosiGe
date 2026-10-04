using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     ორმაგი რეგისტრაციები: მოსწავლე ერთ საგანზე რამდენჯერმე (r20) და მასწავლებელი ერთ ჯგუფში რამდენჯერმე (r21)
/// </summary>
public static class ScheduleDoubleReports
{
    //r20StudentDoubleCources (vR20Base1, vR20StudentDoubleCource): მოსწავლის კონტრაქტი და საგანი, თუ კონტრაქტს ამ
    //საგნის აქტიურ ჯგუფებში ერთზე მეტი მოქმედი სტრიქონი აქვს (ორ ჯგუფში ან ერთში ორჯერ; Access-ის
    //Count(Groups.ID) > 1). რიგი: მოსწავლე "გვარი სახელი / ნომერი", საგანი
    public static ReportTable StudentDoubleCourses(ScheduleSnapshot schedule)
    {
        Dictionary<int, ScheduleGroup> groups = schedule.Groups.ToDictionary(g => g.GroupId);
        return ReportTable.Flat([
            new ReportColumnResponse("student", "მოსწავლე", ReportColumnTypes.Text),
            new ReportColumnResponse("courseName", "საგანი", ReportColumnTypes.Text)
        ], [
            .. schedule.Students
                .GroupBy(s => (s.StudentContractId, groups[s.GroupId].CourseId, groups[s.GroupId].CourseName))
                .Where(registrations => registrations.Count() > 1).Select(registrations => (
                    Student: schedule.StudentNames[registrations.Key.StudentContractId].NameWithNumber,
                    registrations.Key)).OrderBy(d => d.Student, StringComparer.Ordinal)
                .ThenBy(d => d.Key.CourseName, StringComparer.Ordinal)
                .Select(d => new List<object?> { d.Student, d.Key.CourseName })
        ]);
    }

    //r21TeacherDoubleGroups (vR21Base1, vR21TeacherDoubleGroups): მასწავლებლის კონტრაქტი და ჯგუფი, თუ ჯგუფში
    //კონტრაქტს ერთზე მეტი მოქმედი სტრიქონი აქვს. რიგი: მასწავლებელი "გვარი სახელი / ნომერი", ჯგუფი
    public static ReportTable TeacherDoubleGroups(ScheduleSnapshot schedule)
    {
        Dictionary<int, string> groupCodes = schedule.Groups.ToDictionary(g => g.GroupId, g => g.GroupCode);
        return ReportTable.Flat([
            new ReportColumnResponse("teacher", "მასწავლებელი", ReportColumnTypes.Text),
            new ReportColumnResponse("groupCode", "ჯგუფი", ReportColumnTypes.Text)
        ], [
            .. schedule.Teachers.GroupBy(t => (t.TeacherContractId, t.GroupId))
                .Where(registrations => registrations.Count() > 1).Select(registrations => (
                    Teacher: schedule.TeacherNames[registrations.Key.TeacherContractId].NameWithNumber,
                    GroupCode: groupCodes[registrations.Key.GroupId])).OrderBy(d => d.Teacher, StringComparer.Ordinal)
                .ThenBy(d => d.GroupCode, StringComparer.Ordinal)
                .Select(d => new List<object?> { d.Teacher, d.GroupCode })
        ]);
    }
}
