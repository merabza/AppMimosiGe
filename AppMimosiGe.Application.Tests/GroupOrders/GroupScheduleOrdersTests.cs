using System;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//განრიგის ბრძანებები: განრიგის შეცვლა (6) და კვირის საათების შეცვლა (10). კვირის დღეები: 01.09 სამშაბათია (2),
//01.10 და 08.10 ხუთშაბათი (4), 05.10 ორშაბათი (1)
public sealed class GroupScheduleOrdersTests
{
    private const int GroupId = 42;

    //ჯგუფი 01.09-იდან: ორშაბათს 2 საათი (ხაზი 422), მოსწავლეები 20 (ხაზი 21) და 30 (ხაზი 22), 8 საათი / 48 ₾
    private static readonly GroupOrderData Initial =
        InitialOrder(1, GroupId, InitialStudentLine(21, 20), InitialStudentLine(22, 30));

    public static TheoryData<DateTime?, DateTime?, DateTime?> CurrentScheduleEnds => new()
    {
        { Day(12, 15), Day(12, 15), Day(12, 15) },
        { Day(12, 15), Day(12, 31), Day(12, 31) },
        { Day(12, 15), null, null }
    };

    //საწყისი ბრძანების ორშაბათის სტრიქონი
    private static GroupDayTimePlaceRow MondayRow(DateTime? endDate)
    {
        return new GroupDayTimePlaceRow(422, 1, 4, 2f, 1, Day(9, 1), endDate);
    }

    //განრიგის ხაზი; საწყისი ბრძანებისთვის თარიღებით
    private static GroupOrderDayTimePlaceLine Line(int id, int weekDayId, float hoursCount, int roomId = 1,
        int lessonStartTimeId = 4)
    {
        return new GroupOrderDayTimePlaceLine(id, weekDayId, lessonStartTimeId, hoursCount, roomId);
    }

    private static GroupOrderDayTimePlaceLine InitialLine(int id, int weekDayId, float hoursCount, DateTime startDate,
        DateTime? endDate = null)
    {
        return Line(id, weekDayId, hoursCount) with { StartDate = startDate, EndDate = endDate };
    }

    //10-ის მოსწავლის ხაზი: საათების გარეშე (მათ ფორმულა იძლევა), კოეფიციენტით 1
    private static GroupOrderStudentLine HoursLine(int id, int studentContractId, decimal fourWeekFee,
        decimal oneHourFee, string? note = null)
    {
        return new GroupOrderStudentLine(id, studentContractId, null, fourWeekFee, oneHourFee, 1f, note);
    }

    private static GroupOrderData ChangeSchedule(int id, int month, int day,
        params GroupOrderDayTimePlaceLine[] dayTimePlaces)
    {
        return Order(id, EGroupOrderType.ChangeSchedule, Day(month, day), GroupId) with
        {
            DayTimePlaces = dayTimePlaces
        };
    }

    private static GroupOrderData ChangeWeeklyHours(int id, int month, int day,
        GroupOrderDayTimePlaceLine[] dayTimePlaces, params GroupOrderStudentLine[] students)
    {
        return Order(id, EGroupOrderType.ChangeWeeklyHours, Day(month, day), GroupId) with
        {
            DayTimePlaces = dayTimePlaces, Students = students
        };
    }

    //6 ან 10, ორივე ახლანდელი მოსწავლით (10-ს ისინი სჭირდება)
    private static GroupOrderData ScheduleOrder(EGroupOrderType orderType, int month, int day,
        params GroupOrderDayTimePlaceLine[] dayTimePlaces)
    {
        return orderType == EGroupOrderType.ChangeSchedule
            ? ChangeSchedule(2, month, day, dayTimePlaces)
            : ChangeWeeklyHours(2, month, day, dayTimePlaces, HoursLine(61, 20, 48m, 6m), HoursLine(62, 30, 48m, 6m));
    }

    private static GroupState Fold(params GroupOrderData[] orders)
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run(orders);
        Assert.Null(result.NotFitting);
        return result.States[GroupId];
    }

    private static GroupOrderNotFitting? NotFitting(params GroupOrderData[] orders)
    {
        return GroupOrdersFold.Run(orders).NotFitting;
    }

    //D188: a-ზე მოქმედი განრიგი a-თი სრულდება, ახალი a-დან იწყება; მოსწავლეები უცვლელია
    [Fact]
    public void ChangeSchedule_EndsTheCurrentScheduleAndStartsTheNewOne()
    {
        GroupState state = Fold(Initial, ChangeSchedule(2, 10, 1, Line(51, 2, 1f), Line(52, 4, 1f)));

        Assert.Equal(
        [
            MondayRow(Day(10, 1)), new GroupDayTimePlaceRow(51, 2, 4, 1f, 1, Day(10, 1), null),
            new GroupDayTimePlaceRow(52, 4, 4, 1f, 1, Day(10, 1), null)
        ], state.DayTimePlaces);
        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), null), StudentLineRow(22, 30, Day(9, 1), null)],
            state.Students);
    }

    //დაწყების დროის ან ოთახის შეცვლაც განრიგის შეცვლაა
    [Theory]
    [InlineData(5, 1)]
    [InlineData(4, 2)]
    public void ChangeSchedule_OnlyTheTimeOrTheRoom_Fits(int lessonStartTimeId, int roomId)
    {
        GroupState state = Fold(Initial, ChangeSchedule(2, 10, 5, Line(51, 1, 2f, roomId, lessonStartTimeId)));

        Assert.Equal(
            [MondayRow(Day(10, 5)), new GroupDayTimePlaceRow(51, 1, lessonStartTimeId, 2f, roomId, Day(10, 5), null)],
            state.DayTimePlaces);
    }

    //D188: ახალი განრიგი მოქმედს არ უნდა ემთხვეოდეს, 10-შიც (Q-O4)
    [Theory]
    [InlineData(EGroupOrderType.ChangeSchedule)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours)]
    public void SameSchedule_DoesNotFit(EGroupOrderType orderType)
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.ScheduleIsAlreadyCurrent),
            NotFitting(Initial, ScheduleOrder(orderType, 10, 5, Line(51, 1, 2f))));
    }

    //სტრიქონები რიგის მიუხედავად ედრება, საათები კი ზღვრით (D153)
    [Fact]
    public void ChangeSchedule_SameRowsInAnotherOrder_DoesNotFit()
    {
        GroupOrderData initial = Initial with
        {
            DayTimePlaces = [InitialLine(423, 1, 1f, Day(9, 1)), InitialLine(424, 4, 1f, Day(9, 1))]
        };

        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.ScheduleIsAlreadyCurrent),
            NotFitting(initial, ChangeSchedule(2, 10, 1, Line(51, 4, 1.00001f), Line(52, 1, 1f))));
    }

    //D188: 6-ში კვირის საათების ჯამი არ იცვლება: საათები მხოლოდ ბრძანება 10-ით იცვლება
    [Fact]
    public void ChangeSchedule_WeeklyHoursChange_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.WeeklyHoursMustNotChange),
            NotFitting(Initial, ChangeSchedule(2, 10, 1, Line(51, 2, 1f), Line(52, 4, 1.5f))));
    }

    //ჯამი ზღვრით ედრება (D153): 0.99999 + 1.00002 იგივე 2 საათია
    [Fact]
    public void ChangeSchedule_WeeklyHoursWithinTheTolerance_Fits()
    {
        GroupState state = Fold(Initial, ChangeSchedule(2, 10, 1, Line(51, 2, 0.99999f), Line(52, 4, 1.00002f)));

        Assert.Equal(3, state.DayTimePlaces.Count);
    }

    //D188: a-ს კვირის დღე (01.10 ხუთშაბათია) ახალი განრიგის ერთ-ერთი დღე უნდა იყოს
    [Theory]
    [InlineData(EGroupOrderType.ChangeSchedule)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours)]
    public void OrderDateWeekDayNotInTheNewSchedule_DoesNotFit(EGroupOrderType orderType)
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.OrderDateWeekDayIsNotInSchedule),
            NotFitting(Initial, ScheduleOrder(orderType, 10, 1, Line(51, 2, 1f), Line(52, 3, 1f))));
    }

    //D207: a-ზე დაწყებული განრიგის სტრიქონი a-თი ვერ დასრულდება: აქ ჯგუფის პირველ დღეს (01.09, სამშაბათი). ასეთი
    //ცვლილება იმ ბრძანების შეცვლით კეთდება, რომლითაც სტრიქონი დაიწყო
    [Theory]
    [InlineData(EGroupOrderType.ChangeSchedule)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours)]
    public void ScheduleRowStartingOnTheOrderDate_DoesNotFit(EGroupOrderType orderType)
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(Initial, ScheduleOrder(orderType, 9, 1, Line(51, 2, 2f))));
    }

    //D207: იმავე დღის მეორე განრიგის ბრძანება პირველის სტრიქონებს ვერ დაასრულებს
    [Fact]
    public void ChangeSchedule_SecondScheduleOrderOnTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(Initial, ChangeSchedule(2, 10, 1, Line(51, 4, 2f)),
                ChangeSchedule(3, 10, 1, Line(52, 4, 2f, roomId: 2))));
    }

    //D208: ახალი განრიგი ძველის დასრულებას იღებს: საერთოს, სხვადასხვაზე უგვიანესს, ღიას, თუ ერთი მაინც ღიაა
    [Theory]
    [MemberData(nameof(CurrentScheduleEnds))]
    public void ChangeSchedule_TheNewRowsTakeTheEndOfTheCurrentSchedule(DateTime? mondayEnd, DateTime? thursdayEnd,
        DateTime? newEnd)
    {
        GroupOrderData initial = Initial with
        {
            DayTimePlaces =
            [
                InitialLine(423, 1, 1f, Day(9, 1), mondayEnd), InitialLine(424, 4, 1f, Day(9, 1), thursdayEnd)
            ]
        };

        GroupState state = Fold(initial, ChangeSchedule(2, 10, 5, Line(51, 1, 1f, 2), Line(52, 4, 1f, 2)));

        Assert.Equal([Day(10, 5), Day(10, 5), newEnd, newEnd], state.DayTimePlaces.Select(d => d.EndDate));
    }

    //მხოლოდ a-ზე მოქმედი სტრიქონი სრულდება: a-მდე დასრულებული (პარასკევი) და დაგეგმილი (ოთხშაბათი 15.12-იდან)
    //უცვლელია; ახალი სტრიქონი მოქმედის დასრულებას იღებს და დაგეგმილს 15.12-ს უთმობს
    [Fact]
    public void ChangeSchedule_RowsNotActiveOnTheOrderDate_StayUntouched()
    {
        GroupOrderData initial = Initial with
        {
            DayTimePlaces =
            [
                InitialLine(422, 1, 2f, Day(9, 1), Day(12, 15)), InitialLine(423, 5, 1f, Day(9, 1), Day(10, 1)),
                InitialLine(424, 3, 2f, Day(12, 15))
            ]
        };

        GroupState state = Fold(initial, ChangeSchedule(2, 10, 5, Line(51, 1, 2f, 2)));

        Assert.Equal(
        [
            MondayRow(Day(10, 5)), new GroupDayTimePlaceRow(423, 5, 4, 1f, 1, Day(9, 1), Day(10, 1)),
            new GroupDayTimePlaceRow(424, 3, 4, 2f, 1, Day(12, 15), null),
            new GroupDayTimePlaceRow(51, 1, 4, 2f, 2, Day(10, 5), Day(12, 15))
        ], state.DayTimePlaces);
    }

    //D188: 2 სთ → 3 სთ კვირაში (1.5 + 1.5). მოსწავლეების სტრიქონები a-თი სრულდება და ახლები ფორმულის საათებით
    //(8 → 12) და ხაზის ფასით, გადასახადითა და შენიშვნით იწყება (48 ₾ → 72 ₾)
    [Fact]
    public void ChangeWeeklyHours_RecountsTheStudentHours()
    {
        GroupState state = Fold(Initial,
            ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 1.5f), Line(52, 4, 1.5f)], HoursLine(61, 20, 72m, 6m),
                HoursLine(62, 30, 84m, 7m, "ახალი ფასი")));

        Assert.Equal(
        [
            MondayRow(Day(10, 5)), new GroupDayTimePlaceRow(51, 1, 4, 1.5f, 1, Day(10, 5), null),
            new GroupDayTimePlaceRow(52, 4, 4, 1.5f, 1, Day(10, 5), null)
        ], state.DayTimePlaces);
        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(10, 5)), StudentLineRow(22, 30, Day(9, 1), Day(10, 5)),
            new GroupStudentRow(61, 20, 12f, 72m, 6m, 1f, null, Day(10, 5), null),
            new GroupStudentRow(62, 30, 12f, 84m, 7m, 1f, "ახალი ფასი", Day(10, 5), null)
        ], state.Students);
    }

    //D187: კოეფიციენტი ≠ 1: 4 × 1.5 × 3 = 18 საათი
    [Fact]
    public void ChangeWeeklyHours_CoefficientOtherThanOne()
    {
        GroupState state = Fold(Initial,
            ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 1.5f), Line(52, 4, 1.5f)],
                new GroupOrderStudentLine(61, 20, null, 108m, 6m, 1.5f), HoursLine(62, 30, 72m, 6m)));

        Assert.Equal(new GroupStudentRow(61, 20, 18f, 108m, 6m, 1.5f, null, Day(10, 5), null), state.Students[2]);
    }

    //D189: ახალი სტრიქონი ძველის დასრულებას იღებს
    [Fact]
    public void ChangeWeeklyHours_StudentRowEndingLater_TheNewRowTakesItsEnd()
    {
        GroupOrderData initial = InitialOrder(1, GroupId, InitialStudentLine(21, 20, Day(12, 15)));

        GroupState state = Fold(initial, ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 3f)], HoursLine(61, 20, 72m, 6m)));

        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(10, 5)),
            new GroupStudentRow(61, 20, 12f, 72m, 6m, 1f, null, Day(10, 5), Day(12, 15))
        ], state.Students);
    }

    //D209: ჯამი თუ არ იცვლება (2 → 1 + 1), საათებიც უცვლელია: უცვლელი ტარიფით სტრიქონი არ იყოფა და შენიშვნა
    //ადგილზე იწერება, ახალი ფასით კი იყოფა, როგორც 8-ში
    [Fact]
    public void ChangeWeeklyHours_UnchangedTariff_WritesTheNoteInPlace()
    {
        GroupState state = Fold(Initial,
            ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 1f), Line(52, 4, 1f)], HoursLine(61, 20, 48m, 6m, "შენიშვნა"),
                HoursLine(62, 30, 56m, 7m)));

        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), null, note: "შენიშვნა"), StudentLineRow(22, 30, Day(9, 1), Day(10, 5)),
            StudentLineRow(62, 30, Day(10, 5), null, 7m)
        ], state.Students);
    }

    //D188: 10-ში ყველა მოსწავლეა, ვინც a-ზე ჯგუფშია
    [Fact]
    public void ChangeWeeklyHours_CurrentStudentMissing_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.StudentsMustBeAllCurrent),
            NotFitting(Initial, ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 3f)], HoursLine(61, 20, 72m, 6m))));
    }

    [Fact]
    public void ChangeWeeklyHours_StudentNotInTheGroup_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.StudentIsNotInGroup),
            NotFitting(Initial,
                ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 3f)], HoursLine(61, 20, 72m, 6m), HoursLine(62, 30, 72m, 6m),
                    HoursLine(63, 40, 72m, 6m))));
    }

    //a-მდე გასული მოსწავლე ბრძანებაში არ არის; მოსწავლეების გარეშე 10 მხოლოდ განრიგს ცვლის
    [Fact]
    public void ChangeWeeklyHours_NoCurrentStudents_ChangesOnlyTheSchedule()
    {
        GroupOrderData initial = InitialOrder(1, GroupId, InitialStudentLine(21, 20, Day(10, 1)));

        GroupState state = Fold(initial, ChangeWeeklyHours(2, 10, 5, [Line(51, 1, 3f)]));

        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), Day(10, 1))], state.Students);
        Assert.Equal([MondayRow(Day(10, 5)), new GroupDayTimePlaceRow(51, 1, 4, 3f, 1, Day(10, 5), null)],
            state.DayTimePlaces);
    }

    //D207: იმავე დღეს დამატებული მოსწავლის სტრიქონი ახალი საათებით ვერ გაიყოფა (D206, როგორც 8-ში)
    [Fact]
    public void ChangeWeeklyHours_StudentAddedTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(Initial, StudentsOrder(2, EGroupOrderType.AddStudents, Day(10, 5), GroupId, StudentLine(41, 40)),
                ChangeWeeklyHours(3, 10, 5, [Line(51, 1, 3f)], HoursLine(61, 20, 72m, 6m), HoursLine(62, 30, 72m, 6m),
                    HoursLine(63, 40, 72m, 6m))));
    }

    //D185, D187: საათები გატარებისას ითვლება: მოგვიანებით შექმნილი, მაგრამ ადრეული თარიღის 10 (Id 3, 05.10) ადრე
    //შექმნილი 3-ის (Id 2, 08.10) მოსწავლეს ახალი განრიგის საათებს აძლევს (3 საათი → 12)
    [Fact]
    public void AddStudents_AfterAnEarlierWeeklyHoursChange_GetsTheNewHours()
    {
        GroupState state = Fold(Initial,
            StudentsOrder(2, EGroupOrderType.AddStudents, Day(10, 8), GroupId, HoursLine(41, 40, 72m, 6m)),
            ChangeWeeklyHours(3, 10, 5, [Line(51, 1, 3f)], HoursLine(61, 20, 72m, 6m), HoursLine(62, 30, 72m, 6m)));

        Assert.Equal(new GroupStudentRow(41, 40, 12f, 72m, 6m, 1f, null, Day(10, 8), null), state.Students[4]);
    }
}
