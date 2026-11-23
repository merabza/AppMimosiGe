using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupActivityTests
{
    private const string Teacher = "teacher";
    private const string Student = "student";
    private const string Schedule = "schedule";

    //ჯგუფი, რომლის kind სახის ერთადერთი სტრიქონი [startDate, endDate) პერიოდისაა, დანარჩენები 01.09-იდან
    private static GroupState WithRow(string kind, DateTime startDate, DateTime? endDate)
    {
        return kind switch
        {
            Teacher => State(teachers: [TeacherRow(startDate, endDate)]),
            Student => State(students: [StudentRow(startDate, endDate)]),
            _ => State(dayTimePlaces: [DayTimePlaceRow(startDate, endDate)])
        };
    }

    private static bool Contains(GroupActivePeriod period, DateTime day)
    {
        return period.StartDate <= day && (period.EndDate is null || day < period.EndDate);
    }

    //D200: გაუქმების დღეს ჯგუფი უკვე უმოქმედოა, წინა დღეს ჯერ კიდევ მოქმედია
    [Theory]
    [InlineData(10, 31, true)]
    [InlineData(11, 1, false)]
    [InlineData(11, 2, false)]
    public void IsActiveOn_VoidDate_IsTheFirstInactiveDay(int month, int day, bool isActive)
    {
        Assert.Equal(isActive, GroupActivity.IsActiveOn(State(Day(11, 1)), Day(month, day)));
    }

    //[StartDate, EndDate): სტრიქონი დაწყების დღეს უკვე მოქმედებს, დასრულების დღეს აღარ, და თუ ის თავისი სახის
    //ერთადერთია, მასთან ერთად ჯგუფიც
    [Theory]
    [InlineData(Teacher)]
    [InlineData(Student)]
    [InlineData(Schedule)]
    public void IsActiveOn_OnlyRowOfAKind_FromItsStartUntilTheDayBeforeItsEnd(string kind)
    {
        GroupState state = WithRow(kind, Day(10, 15), Day(10, 20));

        Assert.Equal([false, true, true, false],
            new[] { Day(10, 14), Day(10, 15), Day(10, 19), Day(10, 20) }.Select(d => GroupActivity.IsActiveOn(state, d)));
    }

    //D200: მასწავლებლის, მოსწავლის ან განრიგის გარეშე ჯგუფი უმოქმედოა
    [Theory]
    [InlineData(Teacher)]
    [InlineData(Student)]
    [InlineData(Schedule)]
    public void IsActiveOn_NoRowOfAKind_IsNotActive(string kind)
    {
        GroupState state = kind switch
        {
            Teacher => State(teachers: []),
            Student => State(students: []),
            _ => State(dayTimePlaces: [])
        };

        Assert.False(GroupActivity.IsActiveOn(state, Day(10, 1)));
        Assert.Empty(GroupActivity.ActivePeriods(state));
    }

    //დღის დროს მნიშვნელობა არ აქვს
    [Fact]
    public void IsActiveOn_TimeOfTheDay_DoesNotMatter()
    {
        GroupState state = State(Day(11, 1));

        Assert.True(GroupActivity.IsActiveOn(state, Day(10, 31).AddHours(23).AddMinutes(59)));
        Assert.False(GroupActivity.IsActiveOn(state, Day(8, 31).AddHours(23)));
    }

    //D190: ჯგუფი გაუქმების დღიდან დახურულია; გაუქმების თარიღის გარეშე ღიაა
    [Theory]
    [InlineData(10, 31, false)]
    [InlineData(11, 1, true)]
    [InlineData(11, 2, true)]
    public void IsClosedOn_VoidDate_IsTheFirstClosedDay(int month, int day, bool isClosed)
    {
        Assert.Equal(isClosed, GroupActivity.IsClosedOn(State(Day(11, 1)), Day(month, day)));
        Assert.Equal(isClosed, GroupActivity.IsClosedOn(State(Day(11, 1)), Day(month, day).AddHours(23)));
        Assert.False(GroupActivity.IsClosedOn(State(), Day(month, day)));
    }

    //[StartDate, EndDate): სტრიქონი დაწყების დღეს უკვე მოქმედებს, დასრულების დღეს აღარ; დასრულების გარეშე ღიაა
    [Fact]
    public void IsRowActiveOn_FromTheStartUntilTheDayBeforeTheEnd()
    {
        Assert.Equal([false, true, true, false],
            new[] { Day(10, 14), Day(10, 15), Day(10, 19), Day(10, 20) }.Select(d =>
                GroupActivity.IsRowActiveOn(Day(10, 15), Day(10, 20), d)));
        Assert.True(GroupActivity.IsRowActiveOn(Day(10, 15), null, Day(12, 31)));
    }

    [Fact]
    public void ActivePeriods_RowsWithoutEnd_OneEndlessPeriod()
    {
        Assert.Equal([new GroupActivePeriod(Day(9, 1), null)], GroupActivity.ActivePeriods(State()));
    }

    //ჯგუფი მოქმედდება, როცა ბოლო სახის სტრიქონიც იწყება, და მოქმედებს გაუქმებამდე
    [Fact]
    public void ActivePeriods_StartWithTheLatestKindAndEndAtTheVoidDate()
    {
        GroupState state = State(Day(12, 1), dayTimePlaces: [DayTimePlaceRow(Day(9, 7))]);

        Assert.Equal([new GroupActivePeriod(Day(9, 7), Day(12, 1))], GroupActivity.ActivePeriods(state));
    }

    //10.10-იდან 15.10-მდე ჯგუფს მასწავლებელი არ ჰყავს
    [Fact]
    public void ActivePeriods_DaysWithoutATeacher_SplitThePeriods()
    {
        GroupState state = State(teachers: [TeacherRow(Day(9, 1), Day(10, 10)), TeacherRow(Day(10, 15), lineId: 4)]);

        Assert.Equal([new GroupActivePeriod(Day(9, 1), Day(10, 10)), new GroupActivePeriod(Day(10, 15), null)],
            GroupActivity.ActivePeriods(state));
    }

    //ერთმანეთის მომდევნო და გადამფარავი სტრიქონები პერიოდს არ ყოფს
    [Fact]
    public void ActivePeriods_FollowingAndOverlappingRows_OnePeriod()
    {
        GroupState state = State(
            students:
            [
                StudentRow(Day(9, 1), Day(10, 1)), StudentRow(Day(10, 1), Day(11, 1), 4),
                StudentRow(Day(10, 20), Day(12, 1), 5)
            ],
            dayTimePlaces: [DayTimePlaceRow(Day(9, 1), Day(9, 20)), DayTimePlaceRow(Day(9, 20), lineId: 6)]);

        Assert.Equal([new GroupActivePeriod(Day(9, 1), Day(12, 1))], GroupActivity.ActivePeriods(state));
    }

    //სტრიქონი, რომელიც დაწყების დღესვე სრულდება, არცერთ დღეს არ მოქმედებს
    [Fact]
    public void ActivePeriods_RowEndingOnItsStartDay_NeverActive()
    {
        GroupState state = State(teachers: [TeacherRow(Day(9, 10), Day(9, 10))]);

        Assert.Empty(GroupActivity.ActivePeriods(state));
        Assert.Null(GroupActivity.FirstActiveDay(state));
    }

    //D196: მომავალში მოქმედი ჯგუფის პირველი მოქმედი დღე ის მომავალი დღეა, როცა ის მოქმედი ხდება
    [Fact]
    public void FirstActiveDay_FutureGroup_IsTheDayItBecomesActive()
    {
        GroupState state = State(teachers: [TeacherRow(Day(12, 1))]);

        Assert.False(GroupActivity.IsActiveOn(state, Day(11, 22)));
        Assert.Equal(Day(12, 1), GroupActivity.FirstActiveDay(state));
    }

    //ჯგუფი 05.09-იდან მოქმედია, 10.10-იდან მასწავლებლის გარეშეა, 01.12-იდან ისევ მოქმედია
    [Fact]
    public void FirstActiveDay_ActiveAgainLater_IsTheFirstDayOfAll()
    {
        GroupState state = State(teachers: [TeacherRow(Day(9, 5), Day(10, 10)), TeacherRow(Day(12, 1), lineId: 4)]);

        Assert.Equal(Day(9, 5), GroupActivity.FirstActiveDay(state));
    }

    //D200: წესი იგივეა, რაც რეპორტების ActiveGroupsForReports-ისა: ყოველ დღეს ერთი და იგივე ჯგუფებია მოქმედი, და
    //მოქმედი პერიოდები ზუსტად ამ დღეებს მოიცავს
    [Fact]
    public void IsActiveOnAndActivePeriods_MatchActiveGroupsForReports()
    {
        //1 სულ მოქმედია; 2 გაუქმდა 10.09-ს; 3 მასწავლებლის გარეშეა 10.09–14.09; 4-ის მოსწავლეები ერთმანეთს მოსდევს;
        //5-ის განრიგი 05.09–24.09-ია; 6-ის მასწავლებელი არცერთ დღეს; 7-ის მოსწავლე გაუქმების დღიდანაა; 8-ის
        //მასწავლებელი 18.09-იდან, მოსწავლე 30.08–27.09
        Dictionary<int, GroupState> states = new()
        {
            [1] = State(),
            [2] = State(Day(9, 10)),
            [3] = State(teachers: [TeacherRow(Day(9, 1), Day(9, 10)), TeacherRow(Day(9, 15), lineId: 4)]),
            [4] = State(students: [StudentRow(Day(9, 1), Day(9, 10)), StudentRow(Day(9, 10), lineId: 4)]),
            [5] = State(dayTimePlaces: [DayTimePlaceRow(Day(9, 5), Day(9, 25))]),
            [6] = State(teachers: [TeacherRow(Day(9, 12), Day(9, 12))]),
            [7] = State(Day(9, 20), students: [StudentRow(Day(9, 20))]),
            [8] = State(teachers: [TeacherRow(Day(9, 18))], students: [StudentRow(Day(8, 30), Day(9, 28))])
        };
        using var context = new MimosiGeDbContext(
            new DbContextOptionsBuilder<MimosiGeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            true);
        Seed(context, states);

        for (DateTime day = Day(8, 29); day <= Day(10, 2); day = day.AddDays(1))
        {
            List<int> reportGroupIds =
                [.. ActiveGroupsForReports.Query(context, day).Select(g => g.GrpId).OrderBy(id => id)];

            Assert.Equal(reportGroupIds,
                states.Where(s => GroupActivity.IsActiveOn(s.Value, day)).Select(s => s.Key).Order());
            Assert.Equal(reportGroupIds,
                states.Where(s => GroupActivity.ActivePeriods(s.Value).Any(p => Contains(p, day))).Select(s => s.Key)
                    .Order());
        }
    }

    //ჯგუფები თავიანთი სტრიქონებით; carcass-ის SaveChangesAsync-ს domain events dispatcher სჭირდება, ამიტომ
    //სინქრონული SaveChanges
    private static void Seed(MimosiGeDbContext context, Dictionary<int, GroupState> states)
    {
        foreach ((int grpId, GroupState state) in states)
        {
            context.Groups.Add(new Group
            {
                GrpId = grpId,
                GroupCode = $"G{grpId}",
                VoidDate = state.VoidDate,
                GroupsByTeachers =
                [
                    .. state.Teachers.Select(t => new GroupByTeacher
                    {
                        TeacherContractId = t.TeacherContractId,
                        SalarySchemaId = t.SalarySchemaId,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate
                    })
                ],
                GroupsByStudents =
                [
                    .. state.Students.Select(s => new GroupByStudent
                    {
                        StudentContractId = s.StudentContractId, StartDate = s.StartDate, EndDate = s.EndDate
                    })
                ],
                GroupDayTimePlaces =
                [
                    .. state.DayTimePlaces.Select(d => new GroupDayTimePlace
                    {
                        WeekDayId = d.WeekDayId,
                        LessonStartTimeId = d.LessonStartTimeId,
                        HoursCount = d.HoursCount,
                        RoomId = d.RoomId,
                        StartDate = d.StartDate,
                        EndDate = d.EndDate
                    })
                ]
            });
        }

        context.SaveChanges();
        context.ChangeTracker.Clear();
    }
}
