using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Groups.Validation;

public static class GroupMapper
{
    //DirtyLessons აქ არ იცვლება: მას handler-ი რთავს
    public static void ApplyFields(Group group, GroupRequest request)
    {
        group.AcademicYearId = request.AcademicYearId;
        group.GroupCode = request.GroupCode!.Trim();
        group.CourseId = request.CourseId;
        group.GroupSizeId = request.GroupSizeId;
        group.StudentStatusId = request.StudentStatusId;
        group.VoidDate = request.VoidDate?.Date;
    }

    //მოთხოვნის ყოველი არსებული სტრიქონი (Id != 0) ამ ჯგუფს უნდა ეკუთვნოდეს
    public static bool RowsBelongToGroup(Group group, GroupRequest request)
    {
        return AllKnown(request.Teachers.Select(x => x.Id), group.GroupsByTeachers.Select(x => x.Id)) &&
               AllKnown(request.Students.Select(x => x.Id), group.GroupsByStudents.Select(x => x.GbsId)) &&
               AllKnown(request.DayTimePlaces.Select(x => x.Id), group.GroupDayTimePlaces.Select(x => x.GdtpId));
    }

    //ჯგუფის არსებული სტრიქონები, რომლებიც მოთხოვნაში აღარ არის
    public static GroupRemovedRows FindRemovedRows(Group group, GroupRequest request)
    {
        HashSet<int> teacherIds = [.. request.Teachers.Select(x => x.Id)];
        HashSet<int> studentIds = [.. request.Students.Select(x => x.Id)];
        HashSet<int> dayTimePlaceIds = [.. request.DayTimePlaces.Select(x => x.Id)];
        return new GroupRemovedRows([.. group.GroupsByTeachers.Where(x => !teacherIds.Contains(x.Id))],
            [.. group.GroupsByStudents.Where(x => !studentIds.Contains(x.GbsId))],
            [.. group.GroupDayTimePlaces.Where(x => !dayTimePlaceIds.Contains(x.GdtpId))]);
    }

    /// <summary>
    ///     სტრიქონები მოთხოვნიდან: Id 0 ემატება, არსებული იცვლება. ამოშლილ სტრიქონებს handler-ი რეპოზიტორიით შლის.
    ///     defaultSalarySchemes: სქემის გარეშე მოსული მასწავლებლის კონტრაქტის ძირითადი სქემა (კონტრაქტის Id -> სქემა)
    /// </summary>
    public static void ApplyRows(Group group, GroupRequest request, IReadOnlyDictionary<int, int> defaultSalarySchemes)
    {
        Dictionary<int, GroupByTeacher> teachers = group.GroupsByTeachers.ToDictionary(x => x.Id);
        foreach (GroupTeacherRequest row in request.Teachers)
        {
            GroupByTeacher teacher = GetOrAdd(group.GroupsByTeachers, teachers, row.Id);
            teacher.TeacherContractId = row.TeacherContractId;
            teacher.SalarySchemaId = row.SalarySchemaId ?? defaultSalarySchemes[row.TeacherContractId];
            teacher.StartDate = row.StartDate.Date;
            teacher.EndDate = row.EndDate?.Date;
        }

        Dictionary<int, GroupByStudent> students = group.GroupsByStudents.ToDictionary(x => x.GbsId);
        foreach (GroupStudentRequest row in request.Students)
        {
            GroupByStudent student = GetOrAdd(group.GroupsByStudents, students, row.Id);
            student.StudentContractId = row.StudentContractId;
            student.FourWeekHours = row.FourWeekHours;
            student.FourWeekFee = row.FourWeekFee;
            student.OneHourFee = row.OneHourFee;
            student.HoursCoefficient = row.HoursCoefficient;
            student.StartDate = row.StartDate.Date;
            student.EndDate = row.EndDate?.Date;
            student.Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim();
        }

        Dictionary<int, GroupDayTimePlace> dayTimePlaces = group.GroupDayTimePlaces.ToDictionary(x => x.GdtpId);
        foreach (GroupDayTimePlaceRequest row in request.DayTimePlaces)
        {
            GroupDayTimePlace dayTimePlace = GetOrAdd(group.GroupDayTimePlaces, dayTimePlaces, row.Id);
            dayTimePlace.WeekDayId = row.WeekDayId;
            dayTimePlace.LessonStartTimeId = row.LessonStartTimeId;
            dayTimePlace.HoursCount = row.HoursCount;
            dayTimePlace.RoomId = row.RoomId;
            dayTimePlace.StartDate = row.StartDate.Date;
            dayTimePlace.EndDate = row.EndDate?.Date;
        }
    }

    /// <summary>
    ///     გაკვეთილების გენერატორის შემავალი მონაცემები შენახვის შემდეგ (შეცდომიანი გაკვეთილების შესამოწმებლად, D177):
    ///     ჯგუფის შენახული გაკვეთილები (current) და ველები და სტრიქონები მოთხოვნიდან, როგორც მათ ApplyFields და ApplyRows
    ///     შეინახავს. სტრიქონების რიგი შედეგს არ ცვლის, რადგან გადამფარავ პერიოდებს ვალიდატორი არ უშვებს (D55).
    ///     lessonStartTimes: განრიგის დაწყების დრო ID-ით
    /// </summary>
    public static GroupLessonsInput ToLessonsInput(GroupRequest request, GroupLessonsInput current,
        IReadOnlyDictionary<int, int> defaultSalarySchemes, IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return current with
        {
            VoidDate = request.VoidDate?.Date,
            Teachers =
            [
                .. request.Teachers.Select(row => new GeneratorTeacherRow(row.TeacherContractId,
                    row.SalarySchemaId ?? defaultSalarySchemes[row.TeacherContractId], row.StartDate.Date,
                    row.EndDate?.Date))
            ],
            Students =
            [
                .. request.Students.Select(row => new GeneratorStudentRow(row.Id, row.StudentContractId,
                    row.HoursCoefficient, row.FourWeekHours, row.StartDate.Date, row.EndDate?.Date))
            ],
            DayTimePlaces =
            [
                .. request.DayTimePlaces.Select(row => new GeneratorDayTimePlaceRow(row.WeekDayId,
                    lessonStartTimes[row.LessonStartTimeId], row.HoursCount, row.StartDate.Date, row.EndDate?.Date))
            ]
        };
    }

    /// <summary>
    ///     ჯგუფი შენახვის შემდეგ, როგორც მას ApplyFields და ApplyRows შეინახავს, მასწავლებლების დროების შესამოწმებლად
    ///     (GroupScheduleOverlapsRule, D178); მეორე მხარე მოსწავლეებია (D201). grpId: 0 ახალი ჯგუფისთვის;
    ///     lessonStartTimes: განრიგის დაწყების დრო ID-ით
    /// </summary>
    public static GroupSchedule ToTeacherSchedule(int grpId, GroupRequest request,
        IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return ToSchedule(grpId, request,
            request.Teachers.Select(row =>
                new GroupScheduleMember(row.TeacherContractId, row.StartDate.Date, row.EndDate?.Date)),
            request.Students.Select(row => new GroupSchedulePeriod(row.StartDate.Date, row.EndDate?.Date)),
            lessonStartTimes);
    }

    /// <summary>
    ///     ჯგუფი შენახვის შემდეგ, როგორც მას ApplyFields და ApplyRows შეინახავს, მოსწავლეების დროების შესამოწმებლად
    ///     (GroupScheduleOverlapsRule, D179); მეორე მხარე მასწავლებლებია (D201). grpId: 0 ახალი ჯგუფისთვის;
    ///     lessonStartTimes: განრიგის დაწყების დრო ID-ით
    /// </summary>
    public static GroupSchedule ToStudentSchedule(int grpId, GroupRequest request,
        IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return ToSchedule(grpId, request,
            request.Students.Select(row =>
                new GroupScheduleMember(row.StudentContractId, row.StartDate.Date, row.EndDate?.Date)),
            request.Teachers.Select(row => new GroupSchedulePeriod(row.StartDate.Date, row.EndDate?.Date)),
            lessonStartTimes);
    }

    private static GroupSchedule ToSchedule(int grpId, GroupRequest request, IEnumerable<GroupScheduleMember> members,
        IEnumerable<GroupSchedulePeriod> otherSidePeriods, IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return new GroupSchedule(grpId, request.GroupCode!.Trim(), request.VoidDate?.Date, [.. members], [
            .. request.DayTimePlaces.Select(row => new GroupScheduleDayTime(row.WeekDayId,
                lessonStartTimes[row.LessonStartTimeId], row.HoursCount, row.StartDate.Date, row.EndDate?.Date))
        ], [.. otherSidePeriods]);
    }

    private static bool AllKnown(IEnumerable<int> requestedIds, IEnumerable<int> existingIds)
    {
        HashSet<int> existing = [.. existingIds];
        return requestedIds.All(id => id == 0 || existing.Contains(id));
    }

    private static T GetOrAdd<T>(ICollection<T> rows, Dictionary<int, T> existing, int id) where T : new()
    {
        if (id != 0)
        {
            return existing[id];
        }

        var row = new T();
        rows.Add(row);
        return row;
    }
}
