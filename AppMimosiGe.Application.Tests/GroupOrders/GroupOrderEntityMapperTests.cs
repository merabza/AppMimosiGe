using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrderEntityMapperTests
{
    //ორშაბათის 2 საათი და ოთხშაბათის 1.5 საათი 01.09-იდან: კვირაში 3.5 საათი
    private static readonly GroupDayTimePlaceRow[] Schedule =
    [
        new(1, 1, 4, 2f, 1, Day(9, 1), null), new(2, 3, 4, 1.5f, 1, Day(9, 1), null)
    ];

    //შექმნის ბრძანება 7 ჯგუფ 3-ისთვის ხაზებით 101 (მასწავლებელი), 201, 202 (მოსწავლეები) და 301 (განრიგი)
    private static GroupOrder StoredCreateOrder()
    {
        var order = new GroupOrder
        {
            Id = 7,
            OrderType = EGroupOrderType.CreateGroup,
            AcademicYearId = 11,
            OrderDate = Day(9, 1),
            GroupId = 3,
            GroupCode = "601",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            CreatedByUserName = "manager"
        };
        order.GroupOrderTeachers.Add(new GroupOrderTeacher { Id = 101, TeacherContractId = 5, SalarySchemaId = 8 });
        order.GroupOrderStudents.Add(new GroupOrderStudent
        {
            Id = 202, StudentContractId = 21, FourWeekHours = 8, FourWeekFee = 48, OneHourFee = 6, HoursCoefficient = 1
        });
        order.GroupOrderStudents.Add(new GroupOrderStudent
        {
            Id = 201, StudentContractId = 20, FourWeekHours = 8, FourWeekFee = 48, OneHourFee = 6, HoursCoefficient = 1
        });
        order.GroupOrderDayTimePlaces.Add(new GroupOrderDayTimePlace
        {
            Id = 301, WeekDayId = 1, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1
        });
        return order;
    }

    [Fact]
    public void ToData_CreateOrder_HasTheGroupFieldsAndTheLinesInIdOrder()
    {
        GroupOrderData data = GroupOrderEntityMapper.ToData(StoredCreateOrder());

        Assert.Equal(7, data.Id);
        Assert.Equal(EGroupOrderType.CreateGroup, data.OrderType);
        Assert.Equal(3, data.GroupId);
        Assert.Equal(new GroupFields(11, "601", 6, 2, 10), data.Fields);
        Assert.Equal(new GroupOrderTeacherLine(101, 5, 8), Assert.Single(data.Teachers));
        Assert.Equal([201, 202], data.Students.Select(s => s.Id));
        Assert.Equal(new GroupOrderDayTimePlaceLine(301, 1, 4, 2f, 1), Assert.Single(data.DayTimePlaces));
    }

    //ჯგუფის ველები მხოლოდ საწყის და შექმნის ბრძანებას აქვს
    [Fact]
    public void ToData_StudentsOrder_HasNoGroupFields()
    {
        var order = new GroupOrder
        {
            Id = 8,
            OrderType = EGroupOrderType.TransferStudents,
            AcademicYearId = 11,
            OrderDate = Day(10, 1),
            GroupId = 3,
            TargetGroupId = 4,
            CreatedByUserName = "manager"
        };

        GroupOrderData data = GroupOrderEntityMapper.ToData(order);

        Assert.Null(data.Fields);
        Assert.Equal(4, data.TargetGroupId);
    }

    //არსებული ხაზი ადგილზე სწორდება, ახალი ემატება და ახალ ხაზებში ჩანს დროებითი Id-ით, დანარჩენი მოხსნილია
    [Fact]
    public void Write_MatchesTheLinesById()
    {
        GroupOrder order = StoredCreateOrder();
        GroupOrderStudent kept = order.GroupOrderStudents.Single(s => s.Id == 201);
        GroupOrderData data = GroupOrderEntityMapper.ToData(order) with
        {
            Teachers = [],
            Students = [StudentLine(201, 20, 7m, "შენიშვნა"), StudentLine(-1, 22)],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(-1, 3, 4, 1.5f, 2)]
        };

        (GroupOrderNewLines newLines, GroupOrderRemovedLines removed) =
            GroupOrderEntityMapper.Write(order, data, Schedule);

        Assert.Equal([101], removed.Teachers.Select(t => t.Id));
        Assert.Equal([202], removed.Students.Select(s => s.Id));
        Assert.Equal([301], removed.DayTimePlaces.Select(d => d.Id));
        //მოხსნილ ხაზს რეპოზიტორია შლის, ამიტომ ის კოლექციაში რჩება
        Assert.Equal(3, order.GroupOrderStudents.Count);
        Assert.Equal((56m, 7m, "შენიშვნა"), (kept.FourWeekFee!.Value, kept.OneHourFee!.Value, kept.Note));
        GroupOrderStudent added = Assert.Single(newLines.Students).Value;
        Assert.Equal(-1, Assert.Single(newLines.Students).Key);
        Assert.Equal(22, added.StudentContractId);
        Assert.Contains(added, order.GroupOrderStudents);
        Assert.Equal(-1, Assert.Single(newLines.DayTimePlaces).Key);
        Assert.Empty(newLines.Teachers);
    }

    [Fact]
    public void Write_WritesTheOrderFields()
    {
        var order = new GroupOrder { CreatedByUserName = "manager" };
        GroupOrderData data = Order(int.MaxValue, EGroupOrderType.TransferStudents, Day(10, 5), 3) with
        {
            TargetGroupId = 4
        };

        GroupOrderEntityMapper.Write(order, data, Schedule);

        Assert.Equal((EGroupOrderType.TransferStudents, Day(10, 5), false, 3, 4),
            (order.OrderType, order.OrderDate, order.IsPaused, order.GroupId!.Value, order.TargetGroupId!.Value));
        Assert.Null(order.GroupCode);
        Assert.Null(order.VoidDate);
    }

    [Fact]
    public void Write_InitialOrder_WritesTheGroupFieldsAndVoidDate()
    {
        var order = new GroupOrder { CreatedByUserName = "manager" };
        GroupOrderData data = InitialOrder(1, 3) with { VoidDate = Day(12, 20) };

        GroupOrderEntityMapper.Write(order, data, Schedule);

        Assert.Equal(("601", 6, 2, 10, Day(12, 20)),
            (order.GroupCode, order.CourseId!.Value, order.GroupSizeId!.Value, order.StudentStatusId!.Value,
                order.VoidDate!.Value));
    }

    //D187: ხაზის საათები ძრავის ფორმულაა: 4 × კოეფიციენტი × განრიგის კვირის საათები ბრძანების თარიღზე
    [Fact]
    public void Write_StudentLine_GetsTheFormulaHoursOfTheScheduleOnTheOrderDate()
    {
        var order = new GroupOrder { CreatedByUserName = "manager" };
        GroupDayTimePlaceRow[] schedule = [.. Schedule, new GroupDayTimePlaceRow(3, 5, 4, 1f, 1, Day(9, 1), Day(10, 1))];
        GroupOrderData data = StudentsOrder(int.MaxValue, EGroupOrderType.AddStudents, Day(10, 5), 3,
            StudentLine(-1, 20) with { HoursCoefficient = 2f });

        GroupOrderEntityMapper.Write(order, data, schedule);

        Assert.Equal(28f, Assert.Single(order.GroupOrderStudents).FourWeekHours);
    }

    //საწყისი ბრძანების საათები ისტორიაა და ხაზიდან მოდის (D196)
    [Fact]
    public void Write_InitialOrderStudentLine_KeepsItsHours()
    {
        var order = new GroupOrder { CreatedByUserName = "manager" };
        GroupOrderData data = InitialOrder(1, 3, InitialStudentLine(-1, 20) with { FourWeekHours = 5f });

        GroupOrderEntityMapper.Write(order, data, Schedule);

        Assert.Equal((5f, Day(9, 1)),
            (Assert.Single(order.GroupOrderStudents).FourWeekHours!.Value,
                order.GroupOrderStudents.Single().StartDate!.Value));
    }

    //გაყვანის ხაზს ტარიფი არ აქვს
    [Fact]
    public void Write_RemovalLine_HasNoHours()
    {
        var order = new GroupOrder { CreatedByUserName = "manager" };
        GroupOrderData data = StudentsOrder(int.MaxValue, EGroupOrderType.RemoveStudents, Day(10, 5), 3,
            RemovalLine(-1, 20, "წავიდა"));

        GroupOrderEntityMapper.Write(order, data, Schedule);

        GroupOrderStudent line = Assert.Single(order.GroupOrderStudents);
        Assert.Null(line.FourWeekHours);
        Assert.Null(line.FourWeekFee);
        Assert.Equal("წავიდა", line.Note);
    }
}
