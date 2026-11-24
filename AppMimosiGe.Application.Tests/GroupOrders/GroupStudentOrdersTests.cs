using System.Collections.Generic;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//მოსწავლის ბრძანებები ერთ ჯგუფში: დამატება (3), გაყვანა (4) და საფასურის შეცვლა (8)
public sealed class GroupStudentOrdersTests
{
    private const int GroupId = 42;

    //ჯგუფი 01.09-იდან მოსწავლეებით 20 (ხაზი 21) და 30 (ხაზი 22)
    private static readonly GroupOrderData Initial =
        InitialOrder(1, GroupId, InitialStudentLine(21, 20), InitialStudentLine(22, 30));

    private static GroupOrderData Students(int id, EGroupOrderType orderType, int month, int day,
        params GroupOrderStudentLine[] lines)
    {
        return StudentsOrder(id, orderType, Day(month, day), GroupId, lines);
    }

    private static GroupState Fold(params GroupOrderData[] orders)
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run(orders);
        Assert.Null(result.NotFitting);
        return result.States[GroupId];
    }

    private static IReadOnlyList<GroupStudentRow> FoldStudents(params GroupOrderData[] orders)
    {
        return Fold(orders).Students;
    }

    private static GroupOrderNotFitting? NotFitting(params GroupOrderData[] orders)
    {
        return GroupOrdersFold.Run(orders).NotFitting;
    }

    //D190: ახალი სტრიქონები a-დან, ხაზის ტარიფითა და შენიშვნით, დასრულების გარეშე
    [Fact]
    public void AddStudents_StartsTheRowsOnTheOrderDate()
    {
        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), null), StudentLineRow(22, 30, Day(9, 1), null),
            StudentLineRow(41, 40, Day(10, 1), null, 7m, "ახალი"), StudentLineRow(42, 50, Day(10, 1), null)
        ], FoldStudents(Initial,
            Students(2, EGroupOrderType.AddStudents, 10, 1, StudentLine(41, 40, 7m, "ახალი"), StudentLine(42, 50))));
    }

    //D190: მოსწავლე a-ზე ჯგუფში უკვე არ უნდა იყოს
    [Fact]
    public void AddStudents_StudentInTheGroup_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.StudentIsAlreadyInGroup),
            NotFitting(Initial, Students(2, EGroupOrderType.AddStudents, 10, 1, StudentLine(41, 30))));
    }

    //[StartDate, EndDate): დასრულების დღეს მოსწავლე ჯგუფში აღარ არის და ახალი სტრიქონით ბრუნდება
    [Fact]
    public void AddStudents_StudentWhoLeftOnTheOrderDate_ComesBack()
    {
        GroupOrderData initial = InitialOrder(1, GroupId, InitialStudentLine(21, 20, Day(10, 1)));

        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), Day(10, 1)), StudentLineRow(41, 20, Day(10, 1), null)],
            FoldStudents(initial, Students(2, EGroupOrderType.AddStudents, 10, 1, StudentLine(41, 20))));
    }

    //D189, D190: a-ზე მოქმედი სტრიქონი a-თი სრულდება და გაყვანის შენიშვნას იღებს; სხვა მოსწავლე უცვლელია
    [Fact]
    public void RemoveStudents_EndsTheRowAndWritesTheNote()
    {
        Assert.Equal(
            [StudentLineRow(21, 20, Day(9, 1), Day(10, 1), note: "გავიდა"), StudentLineRow(22, 30, Day(9, 1), null)],
            FoldStudents(Initial, Students(2, EGroupOrderType.RemoveStudents, 10, 1, RemovalLine(51, 20, "გავიდა"))));
    }

    //0-ის სტრიქონი, რომელიც გვიან სრულდება, გაყვანის თარიღით მოკლდება
    [Fact]
    public void RemoveStudents_RowEndingLater_EndsOnTheOrderDate()
    {
        GroupOrderData initial = InitialOrder(1, GroupId, InitialStudentLine(21, 20, Day(12, 15)));

        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), Day(10, 1))],
            FoldStudents(initial, Students(2, EGroupOrderType.RemoveStudents, 10, 1, RemovalLine(51, 20))));
    }

    //D190, D200: ყველა მოსწავლის გაყვანის შემდეგ ჯგუფი ღია რჩება, ოღონდ უმოქმედოა
    [Fact]
    public void RemoveStudents_EveryStudent_TheGroupStaysOpenButInactive()
    {
        GroupState state = Fold(Initial,
            Students(2, EGroupOrderType.RemoveStudents, 10, 1, RemovalLine(51, 20), RemovalLine(52, 30)));

        Assert.Null(state.VoidDate);
        Assert.True(GroupActivity.IsActiveOn(state, Day(9, 30)));
        Assert.False(GroupActivity.IsActiveOn(state, Day(10, 1)));
    }

    //4, 8: მოსწავლე a-ზე ჯგუფში უნდა იყოს: 40 ჯგუფში არასოდეს ყოფილა, 20 კი 01.10-ს გავიდა
    [Theory]
    [InlineData(EGroupOrderType.RemoveStudents, 40)]
    [InlineData(EGroupOrderType.RemoveStudents, 20)]
    [InlineData(EGroupOrderType.ChangeStudentFees, 40)]
    [InlineData(EGroupOrderType.ChangeStudentFees, 20)]
    public void StudentNotInTheGroup_DoesNotFit(EGroupOrderType orderType, int studentContractId)
    {
        GroupOrderData initial = InitialOrder(1, GroupId, InitialStudentLine(21, 20, Day(10, 1)));

        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.StudentIsNotInGroup),
            NotFitting(initial, Students(2, orderType, 10, 1, StudentLine(51, studentContractId, 7m))));
    }

    //D206: იმავე დღეს დამატებულ მოსწავლეს გაყვანა ვერ დაასრულებს: ეს დამატების ბრძანების შეცვლით კეთდება
    [Fact]
    public void RemoveStudents_StudentAddedTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(Initial, Students(2, EGroupOrderType.AddStudents, 10, 1, StudentLine(41, 40)),
                Students(3, EGroupOrderType.RemoveStudents, 10, 1, RemovalLine(51, 40))));
    }

    //D189: ორი მოსწავლე ერთი ბრძანებით: ყოველი სტრიქონი a-თი სრულდება და a-დან ახალი ტარიფითა და შენიშვნით იწყება
    [Fact]
    public void ChangeStudentFees_SplitsTheRowsWithTheNewTariffs()
    {
        GroupOrderStudentLine longer = new(62, 30, 12f, 72m, 6m, 1.5f, "1.5 საათი");

        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(10, 1)), StudentLineRow(22, 30, Day(9, 1), Day(10, 1)),
            StudentLineRow(61, 20, Day(10, 1), null, 7m),
            new GroupStudentRow(62, 30, 12f, 72m, 6m, 1.5f, "1.5 საათი", Day(10, 1), null)
        ], FoldStudents(Initial,
            Students(2, EGroupOrderType.ChangeStudentFees, 10, 1, StudentLine(61, 20, 7m), longer)));
    }

    //D189: ახალი სტრიქონი 0-ის სტრიქონის დასრულებას იღებს
    [Fact]
    public void ChangeStudentFees_RowHasAnEnd_TheNewRowTakesIt()
    {
        GroupOrderData initial = InitialOrder(1, GroupId, InitialStudentLine(21, 20, Day(12, 15)));

        Assert.Equal(
            [StudentLineRow(21, 20, Day(9, 1), Day(10, 1)), StudentLineRow(61, 20, Day(10, 1), Day(12, 15), 7m)],
            FoldStudents(initial, Students(2, EGroupOrderType.ChangeStudentFees, 10, 1, StudentLine(61, 20, 7m))));
    }

    //D189: თუ მხოლოდ შენიშვნა იცვლება, სტრიქონი არ იყოფა: შენიშვნა a-ზე მოქმედ სტრიქონში ადგილზე იწერება
    [Fact]
    public void ChangeStudentFees_OnlyTheNote_WritesItInPlace()
    {
        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), null, note: "ახალი შენიშვნა"), StudentLineRow(22, 30, Day(9, 1), null)
        ], FoldStudents(Initial,
            Students(2, EGroupOrderType.ChangeStudentFees, 10, 1, StudentLine(61, 20, note: "ახალი შენიშვნა"))));
    }

    //D206 მხოლოდ სტრიქონის დასრულებას ეხება: შენიშვნა იმავე დღეს დამატებულ მოსწავლესაც ადგილზე ეცვლება
    [Fact]
    public void ChangeStudentFees_OnlyTheNoteOfAStudentAddedTheSameDay_WritesItInPlace()
    {
        Assert.Equal(StudentLineRow(41, 40, Day(10, 1), null, note: "შენიშვნა"),
            FoldStudents(Initial, Students(2, EGroupOrderType.AddStudents, 10, 1, StudentLine(41, 40)),
                Students(3, EGroupOrderType.ChangeStudentFees, 10, 1, StudentLine(61, 40, note: "შენიშვნა")))[2]);
    }

    //D153: საათებისა და კოეფიციენტის სხვაობა float-ის ზღვრის ფარგლებში ტარიფის ცვლილება არ არის
    [Fact]
    public void ChangeStudentFees_FloatDifferenceWithinTheTolerance_IsNoTariffChange()
    {
        GroupOrderStudentLine almostTheSame = new(61, 20, 8.00001f, 48m, 6m, 1.00001f, "შენიშვნა");

        Assert.Equal(
            [StudentLineRow(21, 20, Day(9, 1), null, note: "შენიშვნა"), StudentLineRow(22, 30, Day(9, 1), null)],
            FoldStudents(Initial, Students(2, EGroupOrderType.ChangeStudentFees, 10, 1, almostTheSame)));
    }

    //ტარიფის ცვლილებაა გადასახადის, ფასის ან კოეფიციენტის ცვლილება. საათებს ფორმულა იძლევა (D187): 2-საათიან
    //განრიგში კოეფიციენტი 1.5 მათ 8-დან 12-მდე ზრდის
    [Theory]
    [InlineData(49, 6, 1f, 8f)]
    [InlineData(48, 7, 1f, 8f)]
    [InlineData(72, 6, 1.5f, 12f)]
    public void ChangeStudentFees_AnyTariffPartChanges_SplitsTheRow(int fourWeekFee, int oneHourFee,
        float hoursCoefficient, float fourWeekHours)
    {
        GroupOrderStudentLine line = new(61, 20, null, fourWeekFee, oneHourFee, hoursCoefficient);

        Assert.Equal(
            new GroupStudentRow(61, 20, fourWeekHours, fourWeekFee, oneHourFee, hoursCoefficient, null, Day(10, 1),
                null),
            FoldStudents(Initial, Students(2, EGroupOrderType.ChangeStudentFees, 10, 1, line))[2]);
    }

    //D187: ხაზზე შენახული საათები არ ითვლება: ფორმულა 8 საათს იძლევა, ტარიფი არ იცვლება და შენიშვნა ადგილზე იწერება
    [Fact]
    public void ChangeStudentFees_HoursComeFromTheFormulaNotFromTheLine()
    {
        GroupOrderStudentLine staleHours = new(61, 20, 8.5f, 48m, 6m, 1f, "შენიშვნა");

        Assert.Equal(
            [StudentLineRow(21, 20, Day(9, 1), null, note: "შენიშვნა"), StudentLineRow(22, 30, Day(9, 1), null)],
            FoldStudents(Initial, Students(2, EGroupOrderType.ChangeStudentFees, 10, 1, staleHours)));
    }

    //D187: ახალი მოსწავლის საათები = 4 × კოეფიციენტი × ჯგუფის კვირის საათები (2)
    [Theory]
    [InlineData(1f, 8f)]
    [InlineData(1.5f, 12f)]
    public void AddStudents_HoursComeFromTheFormula(float hoursCoefficient, float fourWeekHours)
    {
        GroupOrderStudentLine line = new(41, 40, null, 48m, 6m, hoursCoefficient);

        Assert.Equal(fourWeekHours,
            FoldStudents(Initial, Students(2, EGroupOrderType.AddStudents, 10, 1, line))[2].FourWeekHours);
    }

    //D206: ახალი ტარიფით სტრიქონი იმ დღეს ვერ გაიყოფა, როცა დაიწყო
    [Fact]
    public void ChangeStudentFees_NewTariffForAStudentAddedTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(Initial, Students(2, EGroupOrderType.AddStudents, 10, 1, StudentLine(41, 40)),
                Students(3, EGroupOrderType.ChangeStudentFees, 10, 1, StudentLine(61, 40, 7m))));
    }
}
