using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupStateMapperTests
{
    private static readonly Dictionary<int, TimeOnly> LessonStartTimes = new() { [4] = new TimeOnly(15, 0) };

    //ჯგუფი 15.12-ს დახურვით: მასწავლებელი 01.09–01.11, მოსწავლეები 20 (ხაზი 2) და 21 (ხაზი 7, 01.10-იდან, შენიშვნით)
    private static GroupState Group()
    {
        return State(Day(12, 15), [TeacherRow(Day(9, 1), Day(11, 1))],
            [StudentRow(Day(9, 1)), StudentLineRow(7, 21, Day(10, 1), null, 7m, "შენიშვნა")]);
    }

    //ბაზის ჯგუფი: სტრიქონები დაწყებისა და Id-ის რიგით, LineId ბაზის სტრიქონის Id-ია (ვიზარდი 1-ის შესავალი)
    [Fact]
    public void ToState_HasTheFieldsAndTheRowsByStartWithTheirIds()
    {
        MimosiGeCore.Domain.Models.Group group = GroupOrdersModeTestData.StoredGroup(4, "605", voidDate: Day(12, 31));
        group.GroupsByStudents.Add(new MimosiGeCore.Domain.Models.GroupByStudent
        {
            GbsId = 40, GroupId = 4, StudentContractId = 50, StartDate = Day(8, 31), EndDate = Day(9, 30)
        });

        GroupState state = GroupStateMapper.ToState(group);

        Assert.Equal((new GroupFields(11, "605", 6, 2, 10), (DateTime?)Day(12, 31)), (state.Fields, state.VoidDate));
        Assert.Equal([new GroupTeacherRow(41, 5, 8, Day(9, 1), null)], state.Teachers);
        Assert.Equal(
        [
            new GroupStudentRow(40, 50, 8f, 48m, 6m, 1f, null, Day(8, 31), Day(9, 30)),
            new GroupStudentRow(42, 24, 8f, 48m, 6m, 1f, "შენიშვნა", Day(9, 1), null),
            new GroupStudentRow(43, 44, 8f, 48m, 6m, 1f, null, Day(9, 1), Day(9, 15))
        ], state.Students);
        Assert.Equal([new GroupDayTimePlaceRow(44, 1, 4, 2f, 1, Day(9, 1), null)], state.DayTimePlaces);
    }

    [Fact]
    public void ToGroupRequest_HasTheFieldsAndEveryRow()
    {
        GroupRequest request = GroupStateMapper.ToGroupRequest(Group());

        Assert.Equal((11, "601", 6, 2, 10, Day(12, 15)),
            (request.AcademicYearId, request.GroupCode, request.CourseId, request.GroupSizeId,
                request.StudentStatusId, request.VoidDate!.Value));
        GroupTeacherRequest teacher = Assert.Single(request.Teachers);
        Assert.Equal((5, 8, Day(9, 1), Day(11, 1)),
            (teacher.TeacherContractId, teacher.SalarySchemaId!.Value, teacher.StartDate, teacher.EndDate!.Value));
        GroupStudentRequest student = request.Students[1];
        Assert.Equal((21, 8f, 56m, 7m, 1f, Day(10, 1), "შენიშვნა"),
            (student.StudentContractId, student.FourWeekHours, student.FourWeekFee, student.OneHourFee,
                student.HoursCoefficient, student.StartDate, student.Note));
        Assert.Null(student.EndDate);
        GroupDayTimePlaceRequest dayTimePlace = Assert.Single(request.DayTimePlaces);
        Assert.Equal((1, 4, 2f, 1, Day(9, 1)),
            (dayTimePlace.WeekDayId, dayTimePlace.LessonStartTimeId, dayTimePlace.HoursCount, dayTimePlace.RoomId,
                dayTimePlace.StartDate));
    }

    //D178, D201: მასწავლებლების შემოწმებისას მეორე მხარე მოსწავლეებია
    [Fact]
    public void ToTeacherSchedule_TeachersAreTheMembersAndStudentsTheOtherSide()
    {
        GroupSchedule schedule = GroupStateMapper.ToTeacherSchedule(3, Group(), LessonStartTimes);

        Assert.Equal((3, "601", Day(12, 15)), (schedule.GrpId, schedule.GroupCode, schedule.VoidDate!.Value));
        Assert.Equal(new GroupScheduleMember(5, Day(9, 1), Day(11, 1)), Assert.Single(schedule.Members));
        Assert.Equal([new GroupSchedulePeriod(Day(9, 1), null), new GroupSchedulePeriod(Day(10, 1), null)],
            schedule.OtherSidePeriods);
        Assert.Equal(new GroupScheduleDayTime(1, new TimeOnly(15, 0), 2f, Day(9, 1), null),
            Assert.Single(schedule.DayTimes));
    }

    //D179, D201: მოსწავლეების შემოწმებისას მეორე მხარე მასწავლებლებია
    [Fact]
    public void ToStudentSchedule_StudentsAreTheMembersAndTeachersTheOtherSide()
    {
        GroupSchedule schedule = GroupStateMapper.ToStudentSchedule(3, Group(), LessonStartTimes);

        Assert.Equal([20, 21], schedule.Members.Select(m => m.ContractId));
        Assert.Equal(new GroupSchedulePeriod(Day(9, 1), Day(11, 1)), Assert.Single(schedule.OtherSidePeriods));
    }

    //D177: არსებული სტრიქონი ბაზის Id-ს ხაზით ინარჩუნებს, ახალს Id 0 აქვს; გაკვეთილები შენახულიდან მოდის
    [Fact]
    public void ToLessonsInput_KeepsTheLessonsAndFindsTheRowIdsByLine()
    {
        var current = new GroupLessonsInput(null, [], [], [], [new ExistingLesson(1, LessonValues(), [])]);

        GroupLessonsInput input = GroupStateMapper.ToLessonsInput(Group(), current,
            new Dictionary<int, int> { [2] = 200 }, LessonStartTimes);

        Assert.Same(current.Lessons, input.Lessons);
        Assert.Equal(Day(12, 15), input.VoidDate);
        Assert.Equal(new GeneratorTeacherRow(5, 8, Day(9, 1), Day(11, 1)), Assert.Single(input.Teachers));
        Assert.Equal(
        [
            new GeneratorStudentRow(200, 20, 1f, 8f, Day(9, 1), null),
            new GeneratorStudentRow(0, 21, 1f, 8f, Day(10, 1), null)
        ], input.Students);
        Assert.Equal(new GeneratorDayTimePlaceRow(1, new TimeOnly(15, 0), 2f, Day(9, 1), null),
            Assert.Single(input.DayTimePlaces));
    }

    private static LessonValues LessonValues()
    {
        return new LessonValues(Day(9, 7), 5, 8, 8f, Day(9, 7), Day(9, 7));
    }
}
