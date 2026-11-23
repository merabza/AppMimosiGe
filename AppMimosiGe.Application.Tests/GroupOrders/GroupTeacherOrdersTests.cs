using System;
using System.Collections.Generic;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//მასწავლებლის ბრძანებები: შეცვლა (2) და ხელფასის სქემის შეცვლა (9)
public sealed class GroupTeacherOrdersTests
{
    private const int GroupId = 42;

    //ჯგუფი 01.09-იდან; თუ მასწავლებლები არ არის მითითებული, მასწავლებელი 5 სქემით 8 (ხაზი 11)
    private static GroupOrderData Initial(params GroupOrderTeacherLine[] teachers)
    {
        return InitialOrder(1, GroupId, InitialStudentLine(21, 20)) with
        {
            Teachers = teachers.Length > 0 ? teachers : [new GroupOrderTeacherLine(11, 5, 8, Day(9, 1))]
        };
    }

    private static GroupOrderData TeacherOrder(int id, EGroupOrderType orderType, DateTime orderDate,
        GroupOrderTeacherLine line)
    {
        return Order(id, orderType, orderDate, GroupId) with { Teachers = [line] };
    }

    private static IReadOnlyList<GroupTeacherRow> FoldTeachers(params GroupOrderData[] orders)
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run(orders);
        Assert.Null(result.NotFitting);
        return result.States[GroupId].Teachers;
    }

    private static GroupOrderNotFitting? NotFitting(params GroupOrderData[] orders)
    {
        return GroupOrdersFold.Run(orders).NotFitting;
    }

    //D190: მოქმედი მასწავლებელი a-თი სრულდება, ახალი a-დან თავისი სქემით იწყება და ღიაა
    [Fact]
    public void ChangeTeacher_EndsTheCurrentTeacherAndStartsTheNewOne()
    {
        Assert.Equal(
            [new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(10, 1)), new GroupTeacherRow(51, 6, 9, Day(10, 1), null)],
            FoldTeachers(Initial(),
                TeacherOrder(2, EGroupOrderType.ChangeTeacher, Day(10, 1), new GroupOrderTeacherLine(51, 6, 9))));
    }

    //D204: 0-ის მასწავლებელი 15.12-ს სრულდება, როცა სხვა მასწავლებელი იწყება: ახალი მასწავლებელი ძველს მხოლოდ
    //15.12-მდე ცვლის, მერე დაგეგმილი მასწავლებელი რჩება
    [Fact]
    public void ChangeTeacher_CurrentRowHasAnEnd_TheNewRowTakesIt()
    {
        GroupOrderData initial = Initial(new GroupOrderTeacherLine(11, 5, 8, Day(9, 1), Day(12, 15)),
            new GroupOrderTeacherLine(12, 7, 8, Day(12, 15)));

        Assert.Equal(
        [
            new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(10, 1)), new GroupTeacherRow(12, 7, 8, Day(12, 15), null),
            new GroupTeacherRow(51, 6, 9, Day(10, 1), Day(12, 15))
        ], FoldTeachers(initial,
            TeacherOrder(2, EGroupOrderType.ChangeTeacher, Day(10, 1), new GroupOrderTeacherLine(51, 6, 9))));
    }

    //D190: მოქმედი მასწავლებლის გარეშე ჯგუფს ახალი მასწავლებელი უბრალოდ ენიშნება, დასრულების გარეშე
    [Fact]
    public void ChangeTeacher_NoCurrentTeacher_AppointsTheNewOne()
    {
        GroupOrderData initial = Initial(new GroupOrderTeacherLine(11, 5, 8, Day(9, 1), Day(10, 1)));

        Assert.Equal(
            [new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(10, 1)), new GroupTeacherRow(51, 6, 9, Day(10, 15), null)],
            FoldTeachers(initial,
                TeacherOrder(2, EGroupOrderType.ChangeTeacher, Day(10, 15), new GroupOrderTeacherLine(51, 6, 9))));
    }

    //D190: დასრულების დღეს მასწავლებელი უკვე აღარ მოქმედებს, ამიტომ იგივე მასწავლებელი შეიძლება დაბრუნდეს
    [Fact]
    public void ChangeTeacher_TeacherWhoLeftOnTheOrderDate_CanComeBack()
    {
        GroupOrderData initial = Initial(new GroupOrderTeacherLine(11, 5, 8, Day(9, 1), Day(10, 1)));

        Assert.Equal(
            [new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(10, 1)), new GroupTeacherRow(51, 5, 8, Day(10, 1), null)],
            FoldTeachers(initial,
                TeacherOrder(2, EGroupOrderType.ChangeTeacher, Day(10, 1), new GroupOrderTeacherLine(51, 5, 8))));
    }

    //D190: ახალი მასწავლებელი მოქმედს არ უნდა ემთხვეოდეს, სხვა სქემითაც: სქემას ბრძანება 9 ცვლის
    [Fact]
    public void ChangeTeacher_SameTeacher_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.TeacherIsAlreadyCurrent),
            NotFitting(Initial(),
                TeacherOrder(2, EGroupOrderType.ChangeTeacher, Day(10, 1), new GroupOrderTeacherLine(51, 5, 9))));
    }

    //D189: იგივე მასწავლებელი a-დან ახალი სქემით
    [Fact]
    public void ChangeSalaryScheme_SplitsTheRowWithTheNewScheme()
    {
        Assert.Equal(
            [new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(10, 1)), new GroupTeacherRow(61, 5, 9, Day(10, 1), null)],
            FoldTeachers(Initial(),
                TeacherOrder(2, EGroupOrderType.ChangeSalaryScheme, Day(10, 1),
                    new GroupOrderTeacherLine(61, 5, 9))));
    }

    //D189: ახალი სტრიქონი 0-ის სტრიქონის დასრულებას იღებს
    [Fact]
    public void ChangeSalaryScheme_CurrentRowHasAnEnd_TheNewRowTakesIt()
    {
        GroupOrderData initial = Initial(new GroupOrderTeacherLine(11, 5, 8, Day(9, 1), Day(12, 15)));

        Assert.Equal(
            [new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(10, 1)), new GroupTeacherRow(61, 5, 9, Day(10, 1), Day(12, 15))],
            FoldTeachers(initial,
                TeacherOrder(2, EGroupOrderType.ChangeSalaryScheme, Day(10, 1),
                    new GroupOrderTeacherLine(61, 5, 9))));
    }

    //9 ჯგუფის მოქმედ მასწავლებელს ეხება: სხვა მასწავლებელზე და მასწავლებლის გარეშე დღეს არ ესადაგება
    [Theory]
    [InlineData(6, 10, 1)]
    [InlineData(5, 10, 15)]
    public void ChangeSalaryScheme_NotTheCurrentTeacher_DoesNotFit(int teacherContractId, int month, int day)
    {
        GroupOrderData initial = Initial(new GroupOrderTeacherLine(11, 5, 8, Day(9, 1), Day(10, 15)));

        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.TeacherIsNotCurrent),
            NotFitting(initial,
                TeacherOrder(2, EGroupOrderType.ChangeSalaryScheme, Day(month, day),
                    new GroupOrderTeacherLine(61, teacherContractId, 9))));
    }

    //9: ახალი სქემა მოქმედს არ უნდა ემთხვეოდეს
    [Fact]
    public void ChangeSalaryScheme_SameScheme_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.SalarySchemeIsAlreadyCurrent),
            NotFitting(Initial(),
                TeacherOrder(2, EGroupOrderType.ChangeSalaryScheme, Day(10, 1),
                    new GroupOrderTeacherLine(61, 5, 8))));
    }

    //D206: a-ზე დაწყებულ მასწავლებლის სტრიქონს ბრძანება a-თი ვერ დაასრულებს: ეს ცვლილება სტრიქონის დამწყებ
    //ბრძანებაში კეთდება (აქ 0-ში მასწავლებელი 7 01.10-იდან)
    [Theory]
    [InlineData(EGroupOrderType.ChangeTeacher, 6)]
    [InlineData(EGroupOrderType.ChangeSalaryScheme, 7)]
    public void TeacherRowStartsOnTheOrderDate_DoesNotFit(EGroupOrderType orderType, int teacherContractId)
    {
        GroupOrderData initial = Initial(new GroupOrderTeacherLine(11, 5, 8, Day(9, 1), Day(10, 1)),
            new GroupOrderTeacherLine(12, 7, 8, Day(10, 1)));

        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(initial,
                TeacherOrder(2, orderType, Day(10, 1), new GroupOrderTeacherLine(51, teacherContractId, 9))));
    }

    //D206: იმავე დღის წინა ბრძანების სტრიქონიც a-ზე იწყება: მასწავლებელი იმავე დღეს მეორედ ვერ შეიცვლება
    [Fact]
    public void ChangeTeacher_TeacherChangedTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(Initial(),
                TeacherOrder(2, EGroupOrderType.ChangeTeacher, Day(10, 1), new GroupOrderTeacherLine(51, 6, 9)),
                TeacherOrder(3, EGroupOrderType.ChangeTeacher, Day(10, 1), new GroupOrderTeacherLine(52, 7, 9))));
    }

    //2 და 9 ერთ მასწავლებელს შეიცავს (ვალიდატორი); სხვა რამ ძრავის შეცდომაა
    [Theory]
    [InlineData(EGroupOrderType.ChangeTeacher)]
    [InlineData(EGroupOrderType.ChangeSalaryScheme)]
    public void TeacherOrderWithoutExactlyOneLine_Throws(EGroupOrderType orderType)
    {
        Assert.Throws<InvalidOperationException>(() =>
            GroupOrdersFold.Run([Initial(), Order(2, orderType, Day(10, 1), GroupId)]));
    }
}
