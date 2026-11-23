using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//მოსწავლეების გადაყვანა (5) ჯგუფიდან Source ჯგუფში Target
public sealed class GroupTransferOrdersTests
{
    private const int Source = 42;
    private const int Target = 43;

    //„საიდან“ 01.09-იდან მოსწავლეებით 20 (ხაზი 21) და 30 (ხაზი 22)
    private static readonly GroupOrderData SourceInitial =
        InitialOrder(1, Source, InitialStudentLine(21, 20), InitialStudentLine(22, 30));

    //„სად“ 01.09-იდან მოსწავლით 40 (ხაზი 31)
    private static readonly GroupOrderData TargetInitial = InitialOrder(2, Target, InitialStudentLine(31, 40));

    private static GroupOrderData Transfer(int id, int month, int day, params GroupOrderStudentLine[] lines)
    {
        return StudentsOrder(id, EGroupOrderType.TransferStudents, Day(month, day), Source, lines) with
        {
            TargetGroupId = Target
        };
    }

    private static GroupOrdersFoldResult Fold(params GroupOrderData[] orders)
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run(orders);
        Assert.Null(result.NotFitting);
        return result;
    }

    private static GroupOrderNotFitting? NotFitting(params GroupOrderData[] orders)
    {
        return GroupOrdersFold.Run(orders).NotFitting;
    }

    //D190: „საიდან“-ში სტრიქონი a-თი სრულდება, „სად“-ში a-დან იწყება ხაზის ტარიფითა და შენიშვნით
    [Fact]
    public void Transfer_EndsTheSourceRowAndStartsTheTargetRow()
    {
        GroupOrdersFoldResult result =
            Fold(SourceInitial, TargetInitial, Transfer(3, 10, 1, StudentLine(71, 20, 7m, "გადავიდა")));

        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), Day(10, 1)), StudentLineRow(22, 30, Day(9, 1), null)],
            result.States[Source].Students);
        Assert.Equal(
            [StudentLineRow(31, 40, Day(9, 1), null), StudentLineRow(71, 20, Day(10, 1), null, 7m, "გადავიდა")],
            result.States[Target].Students);
    }

    //D205: „სად“-ის სტრიქონი „საიდან“-ის სტრიქონის დასრულებას იღებს
    [Fact]
    public void Transfer_SourceRowHasAnEnd_TheTargetRowTakesIt()
    {
        GroupOrderData sourceInitial = InitialOrder(1, Source, InitialStudentLine(21, 20, Day(12, 15)));

        GroupOrdersFoldResult result = Fold(sourceInitial, TargetInitial, Transfer(3, 10, 1, StudentLine(71, 20)));

        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), Day(10, 1))], result.States[Source].Students);
        Assert.Equal(StudentLineRow(71, 20, Day(10, 1), Day(12, 15)), result.States[Target].Students[1]);
    }

    //D190: შეიძლება ყველა მოსწავლის გადაყვანა; „საიდან“ ღია რჩება
    [Fact]
    public void Transfer_EveryStudent_TheSourceStaysOpen()
    {
        GroupOrdersFoldResult result = Fold(SourceInitial, TargetInitial,
            Transfer(3, 10, 1, StudentLine(71, 20), StudentLine(72, 30)));

        Assert.Equal([StudentLineRow(21, 20, Day(9, 1), Day(10, 1)), StudentLineRow(22, 30, Day(9, 1), Day(10, 1))],
            result.States[Source].Students);
        Assert.Null(result.States[Source].VoidDate);
        Assert.Equal(
        [
            StudentLineRow(31, 40, Day(9, 1), null), StudentLineRow(71, 20, Day(10, 1), null),
            StudentLineRow(72, 30, Day(10, 1), null)
        ], result.States[Target].Students);
    }

    //გადაყვანილი მოსწავლე უკან ბრუნდება: ორივე ჯგუფში მისი სტრიქონები ერთმანეთს მიჰყვება
    [Fact]
    public void Transfer_BackToTheSource_ContinuesTheRowsInBothGroups()
    {
        GroupOrderData back = StudentsOrder(4, EGroupOrderType.TransferStudents, Day(11, 1), Target,
            StudentLine(81, 20)) with { TargetGroupId = Source };

        GroupOrdersFoldResult result =
            Fold(SourceInitial, TargetInitial, Transfer(3, 10, 1, StudentLine(71, 20)), back);

        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(10, 1)), StudentLineRow(22, 30, Day(9, 1), null),
            StudentLineRow(81, 20, Day(11, 1), null)
        ], result.States[Source].Students);
        Assert.Equal(StudentLineRow(71, 20, Day(10, 1), Day(11, 1)), result.States[Target].Students[1]);
    }

    //5: მოსწავლე a-ზე „საიდან“-ში უნდა იყოს
    [Fact]
    public void Transfer_StudentNotInTheSource_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.StudentIsNotInGroup),
            NotFitting(SourceInitial, TargetInitial, Transfer(3, 10, 1, StudentLine(71, 40))));
    }

    //„სად“-ში მოსწავლე a-ზე უკვე არ უნდა იყოს, როგორც დამატებისას (3)
    [Fact]
    public void Transfer_StudentAlreadyInTheTarget_DoesNotFit()
    {
        GroupOrderData targetInitial = InitialOrder(2, Target, InitialStudentLine(31, 40), InitialStudentLine(32, 20));

        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.StudentIsAlreadyInGroup),
            NotFitting(SourceInitial, targetInitial, Transfer(3, 10, 1, StudentLine(71, 20))));
    }

    //D190: ჯგუფები ერთი სასწავლო წლისა და საგნისაა
    [Theory]
    [InlineData(12, 6)]
    [InlineData(11, 7)]
    public void Transfer_TargetOfAnotherYearOrCourse_DoesNotFit(int academicYearId, int courseId)
    {
        GroupOrderData targetInitial =
            TargetInitial with { Fields = Fields with { AcademicYearId = academicYearId, CourseId = courseId } };

        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.TargetGroupDoesNotMatch),
            NotFitting(SourceInitial, targetInitial, Transfer(3, 10, 1, StudentLine(71, 20))));
    }

    //ზომა, სტატუსი და კოდი შეიძლება განსხვავდებოდეს
    [Fact]
    public void Transfer_TargetOfAnotherSizeAndStatus_Fits()
    {
        GroupOrderData targetInitial = TargetInitial with
        {
            Fields = Fields with { GroupCode = "602", GroupSizeId = 3, StudentStatusId = 11 }
        };

        Assert.Equal(StudentLineRow(71, 20, Day(10, 1), null),
            Fold(SourceInitial, targetInitial, Transfer(3, 10, 1, StudentLine(71, 20))).States[Target].Students[1]);
    }

    //D190, D202: „სად“ a-ზე ღიაა: გაუქმების დღეს ის უკვე დახურულია, წინა დღეს ჯერ კიდევ ღიაა
    [Theory]
    [InlineData(10, 1, false)]
    [InlineData(10, 2, true)]
    public void Transfer_TargetVoidDate_IsTheFirstClosedDay(int month, int day, bool fits)
    {
        GroupOrderNotFitting? notFitting = NotFitting(SourceInitial,
            TargetInitial with { VoidDate = Day(month, day) }, Transfer(3, 10, 1, StudentLine(71, 20)));

        Assert.Equal(fits ? null : new GroupOrderNotFitting(3, GroupOrderErrors.TargetGroupIsNotOpen), notFitting);
    }

    //D190: „სად“ ბრძანება 7-ით დაიხურა იმავე დღეს, გადაყვანამდე
    [Fact]
    public void Transfer_TargetClosedTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(4, GroupOrderErrors.TargetGroupIsNotOpen),
            NotFitting(SourceInitial, TargetInitial, Order(3, EGroupOrderType.CloseGroup, Day(10, 1), Target),
                Transfer(4, 10, 1, StudentLine(71, 20))));
    }

    //D190: „სად“ ჯერ არ დაწყებულა: მისი შექმნის ბრძანება გადაყვანაზე გვიანაა
    [Fact]
    public void Transfer_TargetCreatedLater_DoesNotFit()
    {
        GroupOrderData targetCreate = Order(2, EGroupOrderType.CreateGroup, Day(10, 15), Target) with
        {
            Fields = Fields,
            Teachers = [new GroupOrderTeacherLine(431, 5, 8)],
            Students = [StudentLine(31, 40)],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(432, 1, 4, 2f, 1)]
        };

        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.TargetGroupIsNotOpen),
            NotFitting(SourceInitial, targetCreate, Transfer(3, 10, 1, StudentLine(71, 20))));
    }

    //D202: ჯგუფს, რომელსაც არც 0 აქვს და არც 1, მოსწავლე ვერ გადაიყვანება
    [Fact]
    public void Transfer_TargetWithoutOrders_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(3, GroupOrderErrors.TargetGroupIsNotOpen),
            NotFitting(SourceInitial, Transfer(3, 10, 1, StudentLine(71, 20))));
    }

    //D200: უმოქმედო (აქ მასწავლებლის გარეშე) ჯგუფიც იღებს მოსწავლეს
    [Fact]
    public void Transfer_InactiveTarget_Fits()
    {
        GroupOrderData targetInitial = TargetInitial with { Teachers = [] };

        GroupOrdersFoldResult result = Fold(SourceInitial, targetInitial, Transfer(3, 10, 1, StudentLine(71, 20)));

        Assert.Equal(StudentLineRow(71, 20, Day(10, 1), null), result.States[Target].Students[1]);
        Assert.False(GroupActivity.IsActiveOn(result.States[Target], Day(10, 1)));
    }

    //D206: იმავე დღეს დამატებული მოსწავლე იმავე დღეს ვერ გადაიყვანება
    [Fact]
    public void Transfer_StudentAddedToTheSourceTheSameDay_DoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(4, GroupOrderErrors.RowStartsOnOrderDate),
            NotFitting(SourceInitial, TargetInitial,
                StudentsOrder(3, EGroupOrderType.AddStudents, Day(10, 1), Source, StudentLine(51, 50)),
                Transfer(4, 10, 1, StudentLine(71, 50))));
    }
}
