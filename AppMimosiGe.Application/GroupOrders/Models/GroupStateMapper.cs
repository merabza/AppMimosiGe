using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის სამიზნე მდგომარეობა (GroupState) ჯგუფის რედაქტორის წესებისთვის (GROUP_ORDERS.md §5 ნაბიჯი 4): ბრძანების
///     შედეგი იმავე წესებს გადის, რასაც რედაქტორის შენახვა, ოღონდ ჯგუფის მოთხოვნას ადამიანი არ აწყობს
/// </summary>
public static class GroupStateMapper
{
    /// <summary>
    ///     ბაზის ჯგუფის ახლანდელი მდგომარეობა სტრიქონებით, დაწყებისა და Id-ის რიგით. სტრიქონის LineId ბაზის სტრიქონის
    ///     Id-ია (GroupByTeacher.Id, GroupByStudent.GbsId, GroupDayTimePlace.GdtpId), როგორც ვიზარდი 1-ის შესავალში
    ///     (GroupOrdersSwitchPlanner)
    /// </summary>
    public static GroupState ToState(Group group)
    {
        return new GroupState(
            new GroupFields(group.AcademicYearId, group.GroupCode, group.CourseId, group.GroupSizeId,
                group.StudentStatusId), group.VoidDate,
            [
                .. group.GroupsByTeachers.OrderBy(t => t.StartDate).ThenBy(t => t.Id).Select(t =>
                    new GroupTeacherRow(t.Id, t.TeacherContractId, t.SalarySchemaId, t.StartDate, t.EndDate))
            ],
            [
                .. group.GroupsByStudents.OrderBy(s => s.StartDate).ThenBy(s => s.GbsId).Select(s =>
                    new GroupStudentRow(s.GbsId, s.StudentContractId, s.FourWeekHours, s.FourWeekFee, s.OneHourFee,
                        s.HoursCoefficient, s.Note, s.StartDate, s.EndDate))
            ],
            [
                .. group.GroupDayTimePlaces.OrderBy(d => d.StartDate).ThenBy(d => d.GdtpId).Select(d =>
                    new GroupDayTimePlaceRow(d.GdtpId, d.WeekDayId, d.LessonStartTimeId, d.HoursCount, d.RoomId,
                        d.StartDate, d.EndDate))
            ]);
    }

    /// <summary>
    ///     ჯგუფის მოთხოვნა GroupRequestValidator-ისთვის (D55, D181-ის 00:00, ცნობარები, დადებითი მნიშვნელობები). სტრიქონებს
    ///     Id არ სჭირდება: ვალიდატორი მათ არ ადარებს
    /// </summary>
    public static GroupRequest ToGroupRequest(GroupState state)
    {
        return new GroupRequest
        {
            AcademicYearId = state.Fields.AcademicYearId,
            GroupCode = state.Fields.GroupCode,
            CourseId = state.Fields.CourseId,
            GroupSizeId = state.Fields.GroupSizeId,
            StudentStatusId = state.Fields.StudentStatusId,
            VoidDate = state.VoidDate,
            Teachers =
            [
                .. state.Teachers.Select(x => new GroupTeacherRequest
                {
                    TeacherContractId = x.TeacherContractId,
                    SalarySchemaId = x.SalarySchemaId,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate
                })
            ],
            Students =
            [
                .. state.Students.Select(x => new GroupStudentRequest
                {
                    StudentContractId = x.StudentContractId,
                    FourWeekHours = x.FourWeekHours,
                    FourWeekFee = x.FourWeekFee,
                    OneHourFee = x.OneHourFee,
                    HoursCoefficient = x.HoursCoefficient,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    Note = x.Note
                })
            ],
            DayTimePlaces =
            [
                .. state.DayTimePlaces.Select(x => new GroupDayTimePlaceRequest
                {
                    WeekDayId = x.WeekDayId,
                    LessonStartTimeId = x.LessonStartTimeId,
                    HoursCount = x.HoursCount,
                    RoomId = x.RoomId,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate
                })
            ]
        };
    }

    /// <summary>
    ///     ჯგუფი მასწავლებლების დროების შესამოწმებლად (GroupScheduleOverlapsRule, D178); მეორე მხარე მოსწავლეებია (D201).
    ///     grpId: ჯგუფის Id (ახალ ჯგუფს დროებითი); lessonStartTimes: განრიგის დაწყების დრო ID-ით
    /// </summary>
    public static GroupSchedule ToTeacherSchedule(int grpId, GroupState state,
        IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return ToSchedule(grpId, state,
            state.Teachers.Select(x => new GroupScheduleMember(x.TeacherContractId, x.StartDate, x.EndDate)),
            state.Students.Select(x => new GroupSchedulePeriod(x.StartDate, x.EndDate)), lessonStartTimes);
    }

    /// <summary>
    ///     ჯგუფი მოსწავლეების დროების შესამოწმებლად (GroupScheduleOverlapsRule, D179); მეორე მხარე მასწავლებლებია (D201)
    /// </summary>
    public static GroupSchedule ToStudentSchedule(int grpId, GroupState state,
        IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return ToSchedule(grpId, state,
            state.Students.Select(x => new GroupScheduleMember(x.StudentContractId, x.StartDate, x.EndDate)),
            state.Teachers.Select(x => new GroupSchedulePeriod(x.StartDate, x.EndDate)), lessonStartTimes);
    }

    /// <summary>
    ///     გაკვეთილების გენერატორის შემავალი მონაცემები ჩაწერის შემდეგ (შეცდომიანი გაკვეთილები, D177): ჯგუფის შენახული
    ///     გაკვეთილები (current) და state-ის სტრიქონები. მოსწავლის სტრიქონი ბაზის სტრიქონს ხაზით ედრება
    ///     (gbsIdsByLineId: GroupOrderStudentId -> GbsId), როგორც მას ჩაწერა შეინარჩუნებს; ახალ სტრიქონს Id 0 აქვს
    /// </summary>
    public static GroupLessonsInput ToLessonsInput(GroupState state, GroupLessonsInput current,
        IReadOnlyDictionary<int, int> gbsIdsByLineId, IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return current with
        {
            VoidDate = state.VoidDate,
            Teachers =
            [
                .. state.Teachers.Select(x =>
                    new GeneratorTeacherRow(x.TeacherContractId, x.SalarySchemaId, x.StartDate, x.EndDate))
            ],
            Students =
            [
                .. state.Students.Select(x => new GeneratorStudentRow(gbsIdsByLineId.GetValueOrDefault(x.LineId),
                    x.StudentContractId, x.HoursCoefficient, x.FourWeekHours, x.StartDate, x.EndDate))
            ],
            DayTimePlaces =
            [
                .. state.DayTimePlaces.Select(x => new GeneratorDayTimePlaceRow(x.WeekDayId,
                    lessonStartTimes[x.LessonStartTimeId], x.HoursCount, x.StartDate, x.EndDate))
            ]
        };
    }

    private static GroupSchedule ToSchedule(int grpId, GroupState state, IEnumerable<GroupScheduleMember> members,
        IEnumerable<GroupSchedulePeriod> otherSidePeriods, IReadOnlyDictionary<int, TimeOnly> lessonStartTimes)
    {
        return new GroupSchedule(grpId, state.Fields.GroupCode, state.VoidDate, [.. members],
        [
            .. state.DayTimePlaces.Select(x => new GroupScheduleDayTime(x.WeekDayId,
                lessonStartTimes[x.LessonStartTimeId], x.HoursCount, x.StartDate, x.EndDate))
        ], [.. otherSidePeriods]);
    }
}
