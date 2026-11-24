using System;
using System.Collections.Generic;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ბეჭდვის მონაცემი ტიპების მიხედვით (GROUP_ORDERS.md §7). ძველი მდგომარეობა ჯგუფი 1-ისაა ბრძანების თარიღზე, 01.12-ზე:
//მასწავლებელი 5 სქემით 8, მოსწავლე 20 (8 საათი, 6 ₾, 48 ₾) და ორშაბათის 2 საათი 15:00-ზე, ოთახ 1-ში
public sealed class GroupOrderPrintTests
{
    private static readonly GroupOrderPrintNames Names = new(
        new Dictionary<int, string> { [5] = "Delta Dan", [6] = "Epsilon Eva" },
        new Dictionary<int, string> { [8] = "ძირითადი", [9] = "საათობრივი" },
        new Dictionary<int, string> { [20] = "Beta Bob", [21] = "Alpha Ann" },
        new Dictionary<int, string> { [1] = "ორშაბათი", [3] = "ოთხშაბათი", [5] = "პარასკევი" },
        new Dictionary<int, string> { [2] = "09:30", [4] = "15:00" },
        new Dictionary<int, string> { [1] = "ოთახი 1", [2] = "ოთახი 2" });

    private static GroupOrderPrintSource Source(GroupOrderData order, string? targetGroupCode = null)
    {
        return new GroupOrderPrintSource(order, new OrderNumber(26, 15), Day(11, 19), "ცოდნის სახლი მიმოსი",
            "დირექტორი", "ნატალია ჭყონია", "601", targetGroupCode, "2026-2027", "მათემატიკა", "4-ოთხი",
            "მეათე კლასი");
    }

    private static GroupOrderPrintResponse Build(GroupOrderData order, GroupState? previous = null,
        string? targetGroupCode = null)
    {
        return GroupOrderPrint.Build(Source(order, targetGroupCode), previous, Names);
    }

    //თავი ბრძანებიდან; განრიგი კვირის დღითა და დაწყების დროით, მოსწავლეები სახელით ლაგდება
    [Fact]
    public void Build_CreateOrder_HasTheHeaderTeacherScheduleAndStudents()
    {
        GroupOrderData order = Order(15, EGroupOrderType.CreateGroup, Day(12, 1), 4) with
        {
            Fields = Fields,
            Teachers = [new GroupOrderTeacherLine(1, 5, 8)],
            Students = [StudentLine(3, 20, note: "ახალი"), StudentLine(2, 21, 7m)],
            DayTimePlaces =
            [
                new GroupOrderDayTimePlaceLine(5, 3, 4, 1.5f, 2), new GroupOrderDayTimePlaceLine(4, 1, 4, 2f, 1),
                new GroupOrderDayTimePlaceLine(6, 1, 2, 1f, 1)
            ]
        };

        GroupOrderPrintResponse print = Build(order);

        Assert.Equal(15, print.Id);
        Assert.Equal((int)EGroupOrderType.CreateGroup, print.OrderType);
        Assert.Equal("26.15", print.Number);
        Assert.Equal(Day(11, 19), print.IssueDate);
        Assert.Equal(Day(12, 1), print.OrderDate);
        Assert.Equal("ცოდნის სახლი მიმოსი", print.OrganizationName);
        Assert.Equal("დირექტორი", print.SignerTitle);
        Assert.Equal("ნატალია ჭყონია", print.SignerName);
        Assert.Equal("601", print.GroupCode);
        Assert.Null(print.TargetGroupCode);
        Assert.Equal("2026-2027", print.AcademicYearName);
        Assert.Equal("მათემატიკა", print.CourseName);
        Assert.Equal("4-ოთხი", print.GroupSizeName);
        Assert.Equal("მეათე კლასი", print.StudentStatusName);
        Assert.Equal(new GroupOrderPrintTeacherResponse("Delta Dan", "ძირითადი"), print.Teacher);
        Assert.Null(print.PreviousTeacher);
        Assert.Equal([
            new GroupOrderPrintDayTimePlaceResponse("ორშაბათი", "09:30", 1f, "ოთახი 1"),
            new GroupOrderPrintDayTimePlaceResponse("ორშაბათი", "15:00", 2f, "ოთახი 1"),
            new GroupOrderPrintDayTimePlaceResponse("ოთხშაბათი", "15:00", 1.5f, "ოთახი 2")
        ], print.DayTimePlaces);
        Assert.Empty(print.PreviousDayTimePlaces);
        Assert.Equal([
            new GroupOrderPrintStudentResponse("Alpha Ann", 8f, 7m, 56m, null, null, null, null),
            new GroupOrderPrintStudentResponse("Beta Bob", 8f, 6m, 48m, "ახალი", null, null, null)
        ], print.Students);
    }

    //2: მასწავლებელი, რომელიც ბრძანების თარიღზე ასწავლიდა, და ახალი
    [Fact]
    public void Build_ChangeTeacher_HasThePreviousAndTheNewTeacher()
    {
        GroupOrderData order = Order(16, EGroupOrderType.ChangeTeacher, Day(12, 1), 1) with
        {
            Teachers = [new GroupOrderTeacherLine(7, 6, 9)]
        };

        GroupOrderPrintResponse print = Build(order, State());

        Assert.Equal(new GroupOrderPrintTeacherResponse("Epsilon Eva", "საათობრივი"), print.Teacher);
        Assert.Equal(new GroupOrderPrintTeacherResponse("Delta Dan", "ძირითადი"), print.PreviousTeacher);
        Assert.Equal([6, 5], GroupOrderPrint.TeacherContractIds(order, State()));
    }

    //D190: მოქმედი მასწავლებლის გარეშე ბრძანება 2 ახალ მასწავლებელს უბრალოდ ნიშნავს
    [Fact]
    public void Build_ChangeTeacherWithoutCurrentTeacher_HasNoPreviousTeacher()
    {
        GroupOrderData order = Order(16, EGroupOrderType.ChangeTeacher, Day(12, 1), 1) with
        {
            Teachers = [new GroupOrderTeacherLine(7, 6, 9)]
        };
        GroupState previous = State(teachers: [TeacherRow(Day(9, 1), Day(11, 1))]);

        GroupOrderPrintResponse print = Build(order, previous);

        Assert.Null(print.PreviousTeacher);
        Assert.Equal([6], GroupOrderPrint.TeacherContractIds(order, previous));
    }

    //9: იგივე მასწავლებელი ძველი და ახალი სქემით
    [Fact]
    public void Build_ChangeSalaryScheme_HasTheOldAndTheNewScheme()
    {
        GroupOrderData order = Order(17, EGroupOrderType.ChangeSalaryScheme, Day(12, 1), 1) with
        {
            Teachers = [new GroupOrderTeacherLine(7, 5, 9)]
        };

        GroupOrderPrintResponse print = Build(order, State());

        Assert.Equal(new GroupOrderPrintTeacherResponse("Delta Dan", "საათობრივი"), print.Teacher);
        Assert.Equal(new GroupOrderPrintTeacherResponse("Delta Dan", "ძირითადი"), print.PreviousTeacher);
        Assert.Equal([5], GroupOrderPrint.TeacherContractIds(order, State()));
    }

    //6: ძველი განრიგი ბრძანების თარიღზე მოქმედი სტრიქონებია
    [Fact]
    public void Build_ChangeSchedule_HasTheOldAndTheNewSchedule()
    {
        GroupOrderData order = Order(18, EGroupOrderType.ChangeSchedule, Day(12, 1), 1) with
        {
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(9, 3, 2, 2f, 2)]
        };
        GroupState previous = State(dayTimePlaces:
            [DayTimePlaceRow(Day(9, 1)), new GroupDayTimePlaceRow(8, 5, 4, 1f, 2, Day(9, 1), Day(11, 1))]);

        GroupOrderPrintResponse print = Build(order, previous);

        Assert.Equal([new GroupOrderPrintDayTimePlaceResponse("ოთხშაბათი", "09:30", 2f, "ოთახი 2")],
            print.DayTimePlaces);
        Assert.Equal([new GroupOrderPrintDayTimePlaceResponse("ორშაბათი", "15:00", 2f, "ოთახი 1")],
            print.PreviousDayTimePlaces);
        Assert.Null(print.Teacher);
        Assert.Null(print.PreviousTeacher);
        Assert.Empty(print.Students);
    }

    //8: ძველი და ახალი ტარიფი; ძველი განრიგი და მასწავლებელი არ იბეჭდება
    [Fact]
    public void Build_ChangeStudentFees_HasTheOldAndTheNewTariff()
    {
        GroupOrderData order = StudentsOrder(19, EGroupOrderType.ChangeStudentFees, Day(12, 1), 1,
            StudentLine(9, 20, 7m, "ფასი შეიცვალა"));

        GroupOrderPrintResponse print = Build(order, State());

        Assert.Equal([new GroupOrderPrintStudentResponse("Beta Bob", 8f, 7m, 56m, "ფასი შეიცვალა", 8f, 6m, 48m)],
            print.Students);
        Assert.Empty(print.PreviousDayTimePlaces);
        Assert.Null(print.PreviousTeacher);
    }

    //ძველი ტარიფი ბრძანების თარიღზე მოქმედი სტრიქონისაა, არა მოსწავლის უფრო ადრეული სტრიქონის
    [Fact]
    public void Build_ChangeStudentFees_TakesTheRowActiveOnTheOrderDate()
    {
        GroupOrderData order = StudentsOrder(19, EGroupOrderType.ChangeStudentFees, Day(12, 1), 1,
            StudentLine(9, 20, 7m));
        GroupState previous = State(students:
            [StudentLineRow(2, 20, Day(9, 1), Day(10, 1), 5m), StudentLineRow(4, 20, Day(10, 1), null)]);

        GroupOrderPrintResponse print = Build(order, previous);

        Assert.Equal([new GroupOrderPrintStudentResponse("Beta Bob", 8f, 7m, 56m, null, 8f, 6m, 48m)],
            print.Students);
    }

    //10: ახალი და ძველი განრიგი, მოსწავლეების ახალი და ძველი საათები და ფასები
    [Fact]
    public void Build_ChangeWeeklyHours_HasTheOldAndTheNewScheduleAndTariffs()
    {
        GroupOrderData order = Order(20, EGroupOrderType.ChangeWeeklyHours, Day(12, 1), 1) with
        {
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(9, 1, 4, 3f, 1)],
            Students = [new GroupOrderStudentLine(10, 20, 12f, 72m, 6m, 1f)]
        };

        GroupOrderPrintResponse print = Build(order, State());

        Assert.Equal([new GroupOrderPrintDayTimePlaceResponse("ორშაბათი", "15:00", 3f, "ოთახი 1")],
            print.DayTimePlaces);
        Assert.Equal([new GroupOrderPrintDayTimePlaceResponse("ორშაბათი", "15:00", 2f, "ოთახი 1")],
            print.PreviousDayTimePlaces);
        Assert.Equal([new GroupOrderPrintStudentResponse("Beta Bob", 12f, 6m, 72m, null, 8f, 6m, 48m)],
            print.Students);
    }

    //4: გაყვანის ხაზს ტარიფი არ აქვს, მხოლოდ შენიშვნა
    [Fact]
    public void Build_RemoveStudents_HasTheStudentsWithTheirNotesOnly()
    {
        GroupOrderData order = StudentsOrder(21, EGroupOrderType.RemoveStudents, Day(12, 1), 1,
            RemovalLine(11, 20, "წავიდა"));

        GroupOrderPrintResponse print = Build(order);

        Assert.Equal([new GroupOrderPrintStudentResponse("Beta Bob", null, null, null, "წავიდა", null, null, null)],
            print.Students);
    }

    //5: „ჯგუფიდან A ჯგუფში B" და მოსწავლეების ახალი ტარიფი
    [Fact]
    public void Build_TransferStudents_HasBothGroupsAndTheNewTariffs()
    {
        GroupOrderData order = StudentsOrder(22, EGroupOrderType.TransferStudents, Day(12, 1), 1,
            StudentLine(12, 20, 7m)) with { TargetGroupId = 2 };

        GroupOrderPrintResponse print = Build(order, targetGroupCode: "602");

        Assert.Equal("601", print.GroupCode);
        Assert.Equal("602", print.TargetGroupCode);
        Assert.Equal([new GroupOrderPrintStudentResponse("Beta Bob", 8f, 7m, 56m, null, null, null, null)],
            print.Students);
    }

    [Fact]
    public void Build_CloseGroup_HasOnlyTheHeader()
    {
        GroupOrderPrintResponse print = Build(Order(23, EGroupOrderType.CloseGroup, Day(12, 1), 1));

        Assert.Equal("601", print.GroupCode);
        Assert.Null(print.Teacher);
        Assert.Null(print.PreviousTeacher);
        Assert.Empty(print.DayTimePlaces);
        Assert.Empty(print.PreviousDayTimePlaces);
        Assert.Empty(print.Students);
    }

    //საწყის ბრძანებას ნომერი არ აქვს და ის არ იბეჭდება (D193)
    [Fact]
    public void Build_WithoutNumber_Throws()
    {
        GroupOrderPrintSource source = Source(Order(24, EGroupOrderType.CloseGroup, Day(12, 1), 1)) with
        {
            Number = null
        };

        Assert.Throws<InvalidOperationException>(() => GroupOrderPrint.Build(source, null, Names));
    }

    [Theory]
    [InlineData(EGroupOrderType.Initial, false)]
    [InlineData(EGroupOrderType.CreateGroup, false)]
    [InlineData(EGroupOrderType.ChangeTeacher, true)]
    [InlineData(EGroupOrderType.AddStudents, false)]
    [InlineData(EGroupOrderType.RemoveStudents, false)]
    [InlineData(EGroupOrderType.TransferStudents, false)]
    [InlineData(EGroupOrderType.ChangeSchedule, true)]
    [InlineData(EGroupOrderType.CloseGroup, false)]
    [InlineData(EGroupOrderType.ChangeStudentFees, true)]
    [InlineData(EGroupOrderType.ChangeSalaryScheme, true)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours, true)]
    public void ShowsPreviousState_OnlyForTheChangeOrders(EGroupOrderType orderType, bool showsPreviousState)
    {
        Assert.Equal(showsPreviousState, GroupOrderPrint.ShowsPreviousState(orderType));
    }

    //ძველი მასწავლებელი მხოლოდ 2-სა და 9-ში იბეჭდება, ამიტომ სხვა ბრძანებას მისი სახელი არ სჭირდება
    [Fact]
    public void TeacherContractIds_OtherOrders_HaveOnlyTheirOwnTeacher()
    {
        GroupOrderData order = Order(25, EGroupOrderType.ChangeSchedule, Day(12, 1), 1);

        Assert.Empty(GroupOrderPrint.TeacherContractIds(order, State()));
    }

    [Fact]
    public void StudentContractIds_AreTheDistinctStudentsOfTheLines()
    {
        GroupOrderData order = StudentsOrder(26, EGroupOrderType.AddStudents, Day(12, 1), 1, StudentLine(1, 21),
            StudentLine(2, 20), StudentLine(3, 21));

        Assert.Equal([21, 20], GroupOrderPrint.StudentContractIds(order));
    }
}
