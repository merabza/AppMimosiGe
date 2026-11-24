using System;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდი 1-ის დამგეგმავი (D194, D196, D200, D202): რომელი ჯგუფი იღებს საწყის ბრძანებას და რა თარიღით, რას შეიცავს
//ის და რა დარღვევებს აჩვენებს რეპორტი
public sealed class GroupOrdersSwitchPlannerTests
{
    private const int GroupId = 42;

    //გადასვლის დღე (D0)
    private static readonly DateTime D0 = Day(10, 1);

    private static string StudentName(int studentContractId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"მოსწავლე {studentContractId}");
    }

    private static DateTime? OrderDate(GroupState state)
    {
        return GroupOrdersSwitchPlanner.InitialOrderDate(state, D0);
    }

    private static GroupOrdersSwitchPlan Plan(params (int Id, GroupState State)[] groups)
    {
        return GroupOrdersSwitchPlanner.Plan(groups.ToDictionary(g => g.Id, g => g.State), D0, StudentName);
    }

    //ჯგუფი კოდით და სასწავლო წლით
    private static GroupState Coded(string groupCode, GroupState state, int academicYearId = 11)
    {
        return state with { Fields = state.Fields with { GroupCode = groupCode, AcademicYearId = academicYearId } };
    }

    private static GroupDayTimePlaceRow Schedule(float hoursCount, DateTime startDate, DateTime? endDate = null,
        int lineId = 3)
    {
        return new GroupDayTimePlaceRow(lineId, 1, 4, hoursCount, 1, startDate, endDate);
    }

    //ჯგუფი ისტორიით: მასწავლებელი და განრიგი 01.10-ს შეიცვალა, მეორე მოსწავლე 15.09-ს გავიდა, ჯგუფი 31.12-ს უქმდება.
    //ის 03.09-იდან მოქმედია, როცა მოსწავლეებიც დაიწყეს
    private static GroupState History()
    {
        return new GroupState(Fields, Day(12, 31),
            [new GroupTeacherRow(11, 5, 8, Day(9, 2), Day(10, 1)), new GroupTeacherRow(12, 6, 9, Day(10, 1), null)],
            [
                new GroupStudentRow(21, 20, 12f, 72m, 6m, 1.5f, "შენიშვნა", Day(9, 3), null),
                new GroupStudentRow(22, 30, 8f, 48m, 6m, 1f, null, Day(9, 3), Day(9, 15))
            ],
            [
                new GroupDayTimePlaceRow(31, 1, 4, 2f, 1, Day(9, 1), Day(10, 1)),
                new GroupDayTimePlaceRow(32, 3, 5, 2f, 2, Day(10, 1), null)
            ]);
    }

    //D196: D0-ზე მოქმედი ჯგუფის საწყისი ბრძანება მისი პირველი მოქმედი დღისაა, წარსულში
    [Fact]
    public void InitialOrderDate_GroupActiveOnD0_IsItsFirstActiveDayInThePast()
    {
        Assert.Equal(Day(9, 1), OrderDate(State()));
    }

    //D196, D200: ჯგუფი მოქმედი ხდება, როცა მასწავლებელიც, მოსწავლეც და განრიგიც დაიწყო
    [Fact]
    public void InitialOrderDate_RowsStartingOnDifferentDays_IsTheDayTheGroupBecomesActive()
    {
        GroupState state = State(teachers: [TeacherRow(Day(9, 2))], students: [StudentRow(Day(9, 4))],
            dayTimePlaces: [DayTimePlaceRow(Day(9, 3))]);

        Assert.Equal(Day(9, 4), OrderDate(state));
    }

    //D196: მომავალში მოქმედი ჯგუფის საწყისი ბრძანება იმ მომავალი დღისაა, როცა ჯგუფი მოქმედი გახდება
    [Fact]
    public void InitialOrderDate_GroupActiveOnlyInTheFuture_IsTheDayItBecomesActive()
    {
        GroupState state = State(teachers: [TeacherRow(Day(10, 20))], students: [StudentRow(Day(10, 15))],
            dayTimePlaces: [DayTimePlaceRow(Day(10, 15))]);

        Assert.Equal(Day(10, 20), OrderDate(state));
    }

    //D196, D202: D0-მდე მოქმედი ჯგუფი, რომელიც D0-დან აღარასოდეს იქნება მოქმედი, საწყის ბრძანებას არ იღებს (როგორც
    //1103 და 1211: მასწავლებელი დასრულდა, მოსწავლე და განრიგი დარჩა). D0-ს დასრულებული მასწავლებელი D0-ს აღარ არის,
    //ერთი დღით გვიან დასრულებული კი ჯერ კიდევ არის
    [Theory]
    [InlineData(9, 20, false)]
    [InlineData(10, 1, false)]
    [InlineData(10, 2, true)]
    public void InitialOrderDate_GroupNoLongerActiveFromD0_IsNull(int month, int day, bool hasOrder)
    {
        DateTime? expected = hasOrder ? Day(9, 1) : null;

        Assert.Equal(expected, OrderDate(State(teachers: [TeacherRow(Day(9, 1), Day(month, day))])));
    }

    //D200: ჯგუფი, რომელიც არცერთ დღეს არ არის მოქმედი, საწყის ბრძანებას არ იღებს: მასწავლებელი არ ჰყავს, ან
    //მასწავლებელი და მოსწავლე ერთმანეთის მერე არიან და არცერთ დღეს ერთად
    [Fact]
    public void InitialOrderDate_GroupNeverActive_IsNull()
    {
        Assert.Null(OrderDate(State(teachers: [])));
        Assert.Null(OrderDate(State(teachers: [TeacherRow(Day(9, 1), Day(9, 10))],
            students: [StudentRow(Day(9, 10))])));
    }

    //D190, D200: დახურული ჯგუფი საწყის ბრძანებას არ იღებს. VoidDate = D0-ით ჯგუფი D0-ს უკვე დახურულია, ერთი დღით
    //გვიან გაუქმებული კი D0-ს ჯერ კიდევ მოქმედია
    [Theory]
    [InlineData(9, 20, false)]
    [InlineData(10, 1, false)]
    [InlineData(10, 2, true)]
    public void InitialOrderDate_VoidDate_ClosesTheGroupFromThatDay(int month, int day, bool hasOrder)
    {
        DateTime? expected = hasOrder ? Day(9, 1) : null;

        Assert.Equal(expected, OrderDate(State(Day(month, day))));
    }

    //D196: ჯგუფი, რომელიც D0-ს უმოქმედოა, მაგრამ მოგვიანებით ისევ მოქმედი გახდება (20.09–15.10 მასწავლებელი არ ჰყავს),
    //საწყის ბრძანებას იღებს, თავისი პირველი მოქმედი დღით, წარსულში
    [Fact]
    public void InitialOrderDate_GroupInactiveOnD0ButActiveLater_IsItsFirstActiveDay()
    {
        GroupState state = State(teachers: [TeacherRow(Day(9, 1), Day(9, 20)), TeacherRow(Day(10, 15), lineId: 4)]);

        Assert.Equal(Day(9, 1), OrderDate(state));
    }

    //D196: საწყისი ბრძანება შეიცავს ჯგუფის ველებს, გაუქმების თარიღს და ყველა სტრიქონს, დასრულებულებსაც, თავისი
    //თარიღებით. ხაზს სტრიქონის Id აქვს: ამით უკავშირდება სტრიქონი თავის ხაზს. ნომერი და დაპაუზება არ აქვს
    [Fact]
    public void InitialOrder_HoldsTheFieldsAndEveryRowWithItsDates()
    {
        GroupOrderData? order = GroupOrdersSwitchPlanner.InitialOrder(GroupId, History(), D0);

        Assert.NotNull(order);
        Assert.Equal(GroupId, order.Id);
        Assert.Equal(EGroupOrderType.Initial, order.OrderType);
        Assert.Equal(Day(9, 3), order.OrderDate);
        Assert.Equal(GroupId, order.GroupId);
        Assert.Null(order.TargetGroupId);
        Assert.False(order.IsPaused);
        Assert.Equal(Fields, order.Fields);
        Assert.Equal(Day(12, 31), order.VoidDate);
        Assert.Equal(
        [
            new GroupOrderTeacherLine(11, 5, 8, Day(9, 2), Day(10, 1)),
            new GroupOrderTeacherLine(12, 6, 9, Day(10, 1))
        ], order.Teachers);
        Assert.Equal(
        [
            new GroupOrderStudentLine(21, 20, 12f, 72m, 6m, 1.5f, "შენიშვნა", Day(9, 3)),
            new GroupOrderStudentLine(22, 30, 8f, 48m, 6m, 1f, null, Day(9, 3), Day(9, 15))
        ], order.Students);
        Assert.Equal(
        [
            new GroupOrderDayTimePlaceLine(31, 1, 4, 2f, 1, Day(9, 1), Day(10, 1)),
            new GroupOrderDayTimePlaceLine(32, 3, 5, 2f, 2, Day(10, 1))
        ], order.DayTimePlaces);
    }

    //საწყისი ბრძანება იმავე ჯგუფს იძლევა: ყოველი სტრიქონი თავისი ხაზით (LineId), ცვლილების გარეშე
    [Fact]
    public void InitialOrder_FoldsIntoTheGroupItCameFrom()
    {
        GroupState state = History();

        GroupState folded = GroupOrderEffects.Apply(null, GroupOrdersSwitchPlanner.InitialOrder(GroupId, state, D0)!)
            .Value;

        Assert.Equal(state.Fields, folded.Fields);
        Assert.Equal(state.VoidDate, folded.VoidDate);
        Assert.Equal(state.Teachers, folded.Teachers);
        Assert.Equal(state.Students, folded.Students);
        Assert.Equal(state.DayTimePlaces, folded.DayTimePlaces);
    }

    [Fact]
    public void InitialOrder_GroupNotActiveFromD0_IsNull()
    {
        Assert.Null(GroupOrdersSwitchPlanner.InitialOrder(GroupId, State(Day(9, 20)), D0));
    }

    //D194, D196: რეპორტი ყოველ ჯგუფს ჩამოთვლის: ვინ მიიღებს საწყის ბრძანებას რა თარიღით და ვინ ვერ მიიღებს
    [Fact]
    public void Plan_ListsEveryGroupWithItsInitialOrderDate()
    {
        (int, string, DateTime?)[] expected =
            [(1, "601", Day(9, 1)), (2, "602", Day(10, 20)), (3, "603", null), (4, "604", null), (5, "605", null)];

        GroupOrdersSwitchPlan plan = Plan(
            (1, Coded("601", State())),
            (2, Coded("602", State(teachers: [TeacherRow(Day(10, 20))]))),
            (3, Coded("603", State(teachers: [TeacherRow(Day(9, 1), Day(9, 20))]))),
            (4, Coded("604", State(Day(9, 20)))),
            (5, Coded("605", State(D0))));

        Assert.Empty(plan.Violations);
        Assert.Equal(expected, plan.Groups.Select(g => (g.GroupId, g.GroupCode, g.InitialOrder?.OrderDate)));
    }

    //ჯგუფები რიგდება სასწავლო წლით, კოდით (ტექსტად) და Id-ით
    [Fact]
    public void Plan_OrdersTheGroupsByAcademicYearCodeAndId()
    {
        GroupOrdersSwitchPlan plan = Plan(
            (5, Coded("601", State())),
            (3, Coded("1015", State())),
            (4, Coded("701", State(), 10)),
            (2, Coded("601", State())));

        Assert.Equal([4, 3, 2, 5], plan.Groups.Select(g => g.GroupId));
    }

    //D187, D194: ფორმულები D0-დან მოწმდება. 1015-ის განრიგი 3 საათია, მოსწავლეს კი 8 საათი აქვს (უნდა იყოს 12):
    //დარღვევა D0-დანაა, წესი ამ სტრიქონის შენახვის შეცდომაა, სტრიქონი კი მოსწავლის სახელი. ჯგუფი საწყის ბრძანებას
    //მაინც იღებს: დარღვევები ჯგუფების სიას არ ცვლის
    [Fact]
    public void Plan_HoursFormulaViolatedFromD0_IsReported()
    {
        GroupOrdersSwitchPlan plan = Plan((1, Coded("1015", State(dayTimePlaces: [Schedule(3f, Day(9, 1))]))));

        Assert.Equal(
        [
            new GroupOrdersSwitchViolation(1, "1015",
                GroupOrderErrors.StudentHoursDoNotMatchSchedule(
                    ["მოსწავლე 20 — ჯგუფი 1015, 01.10.2026-დან: 4 კვირაში 8 საათი, უნდა იყოს 4 × 1 × 3 = 12"]), D0,
                "მოსწავლე 20")
        ], plan.Violations);
        Assert.Equal(Day(9, 1), Assert.Single(plan.Groups).InitialOrder?.OrderDate);
    }

    //D187, D194: 4 კვირის გადასახადი 50-ია, საათის ფასი 6 და 8 საათი კი 48-ს იძლევა
    [Fact]
    public void Plan_FeeFormulaViolatedFromD0_IsReported()
    {
        GroupStudentRow row = new(2, 20, 8f, 50m, 6m, 1f, null, Day(9, 1), null);

        GroupOrdersSwitchPlan plan = Plan((1, Coded("1015", State(students: [row]))));

        Assert.Equal(
        [
            new GroupOrdersSwitchViolation(1, "1015",
                GroupOrderErrors.StudentFeeDoesNotMatchHours(
                    ["მოსწავლე 20 — ჯგუფი 1015, 01.10.2026-დან: 4 კვირის გადასახადი 50, უნდა იყოს 6 × 8 = 48"]), D0,
                "მოსწავლე 20")
        ], plan.Violations);
    }

    //D194: D0-მდე ისტორია არ მოწმდება: სექტემბერში განრიგი 3 საათი იყო (ფორმულით 12), D0-დან კი 2-ია (8)
    [Fact]
    public void Plan_FormulasBeforeD0_AreNotChecked()
    {
        GroupOrdersSwitchPlan plan =
            Plan((1, State(dayTimePlaces: [Schedule(3f, Day(9, 1), D0), Schedule(2f, D0, lineId: 4)])));

        Assert.Empty(plan.Violations);
    }

    //D194: მოწმდება მხოლოდ ჯგუფი, რომელიც საწყის ბრძანებას მიიღებს. D0-ს დახურული ჯგუფის და D0-მდე დასრულებული
    //მასწავლებლის ჯგუფის ფორმულა (3-საათიანი განრიგი, 8 საათი) არ მოწმდება
    [Fact]
    public void Plan_GroupsWithoutInitialOrder_AreNotChecked()
    {
        GroupDayTimePlaceRow[] wrongHours = [Schedule(3f, Day(9, 1))];

        GroupOrdersSwitchPlan plan = Plan(
            (1, Coded("601", State(D0, dayTimePlaces: wrongHours))),
            (2, Coded("602", State(teachers: [TeacherRow(Day(9, 1), Day(9, 20))], dayTimePlaces: wrongHours))));

        Assert.Empty(plan.Violations);
        Assert.All(plan.Groups, g => Assert.Null(g.InitialOrder));
    }

    //D194: მომავალში მოქმედი ჯგუფიც მოწმდება, თავისი სტრიქონების დაწყებიდან
    [Fact]
    public void Plan_FutureGroup_IsCheckedFromItsStart()
    {
        GroupOrdersSwitchPlan plan = Plan((1, State(teachers: [TeacherRow(Day(10, 15))],
            students: [StudentRow(Day(10, 15))], dayTimePlaces: [Schedule(3f, Day(10, 15))])));

        Assert.Equal([Day(10, 15)], plan.Violations.Select(v => v.Date));
    }

    //დარღვევები ჯგუფების რიგითაა, ჯგუფში კი დღით: 601-ის დარღვევა 1015-ის დარღვევებზე ადრეა, მაგრამ 1015 რიგში
    //პირველია
    [Fact]
    public void Plan_Violations_FollowTheGroupsOrderAndThenTheDay()
    {
        (int, DateTime?)[] expected = [(2, Day(10, 15)), (2, Day(11, 1)), (1, D0)];
        GroupDayTimePlaceRow[] wrongHours = [Schedule(3f, Day(9, 1))];

        GroupOrdersSwitchPlan plan = Plan(
            (1, Coded("601", State(dayTimePlaces: wrongHours))),
            (2, Coded("1015", State(
                students: [StudentRow(Day(11, 1)), StudentRow(Day(10, 15), lineId: 5) with { StudentContractId = 30 }],
                dayTimePlaces: wrongHours))));

        Assert.Equal(expected, plan.Violations.Select(v => (v.GroupId, v.Date)));
    }
}
