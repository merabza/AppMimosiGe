using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrderRequestMapperTests
{
    private static readonly Dictionary<int, int> NoDefaultSchemes = [];

    //შექმნის ბრძანება: ჯგუფის ველები, მასწავლებელი, მოსწავლე და განრიგი, ყველა ხაზი ახალია
    private static GroupOrderRequest CreateRequest(List<GroupOrderTeacherRequest>? teachers = null,
        List<GroupOrderStudentRequest>? students = null, List<GroupOrderDayTimePlaceRequest>? dayTimePlaces = null)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.CreateGroup,
            OrderDate = Day(12, 1),
            AcademicYearId = 11,
            GroupCode = "601",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            Teachers = teachers ?? [new GroupOrderTeacherRequest { TeacherContractId = 5, SalarySchemaId = 8 }],
            Students = students ?? [Student(0, 20)],
            DayTimePlaces = dayTimePlaces ?? [DayTimePlace(0)]
        };
    }

    private static GroupOrderStudentRequest Student(int id, int studentContractId, string? note = null)
    {
        return new GroupOrderStudentRequest
        {
            Id = id,
            StudentContractId = studentContractId,
            FourWeekFee = 48,
            OneHourFee = 6,
            HoursCoefficient = 1,
            Note = note
        };
    }

    private static GroupOrderDayTimePlaceRequest DayTimePlace(int id)
    {
        return new GroupOrderDayTimePlaceRequest
        {
            Id = id, WeekDayId = 2, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1
        };
    }

    //ბაზის ბრძანება ხაზებით 101 (მასწავლებელი), 201, 202 (მოსწავლეები) და 301 (განრიგი)
    private static GroupOrder StoredOrder()
    {
        var order = new GroupOrder { Id = 7, CreatedByUserName = "manager" };
        order.GroupOrderTeachers.Add(new GroupOrderTeacher { Id = 101 });
        order.GroupOrderStudents.Add(new GroupOrderStudent { Id = 201 });
        order.GroupOrderStudents.Add(new GroupOrderStudent { Id = 202 });
        order.GroupOrderDayTimePlaces.Add(new GroupOrderDayTimePlace { Id = 301 });
        return order;
    }

    [Fact]
    public void IsPaused_CreateOrderWithTeacherAndSchedule_IsFalse()
    {
        Assert.False(GroupOrderRequestMapper.IsPaused(CreateRequest()));
    }

    //D186: მასწავლებლის ან განრიგის გარეშე შექმნის ბრძანება ჯგუფს ჯერ არ ქმნის
    [Fact]
    public void IsPaused_CreateOrderWithoutTeacher_IsTrue()
    {
        Assert.True(GroupOrderRequestMapper.IsPaused(CreateRequest(teachers: [])));
    }

    [Fact]
    public void IsPaused_CreateOrderWithoutSchedule_IsTrue()
    {
        Assert.True(GroupOrderRequestMapper.IsPaused(CreateRequest(dayTimePlaces: [])));
    }

    //დაპაუზდება მხოლოდ შექმნის ბრძანება
    [Fact]
    public void IsPaused_OtherOrderWithoutTeacherAndSchedule_IsFalse()
    {
        Assert.False(GroupOrderRequestMapper.IsPaused(new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.AddStudents, GroupId = 1, Students = [Student(0, 20)]
        }));
    }

    [Fact]
    public void LinesBelongToOrder_NewOrderWithNewLines_IsTrue()
    {
        Assert.True(GroupOrderRequestMapper.LinesBelongToOrder(CreateRequest(), null));
    }

    [Fact]
    public void LinesBelongToOrder_NewOrderWithAnExistingLineId_IsFalse()
    {
        Assert.False(GroupOrderRequestMapper.LinesBelongToOrder(CreateRequest(students: [Student(201, 20)]), null));
    }

    [Fact]
    public void LinesBelongToOrder_ItsOwnAndNewLines_IsTrue()
    {
        GroupOrderRequest request = CreateRequest(
            [new GroupOrderTeacherRequest { Id = 101, TeacherContractId = 5 }],
            [Student(202, 21), Student(0, 22)], [DayTimePlace(301)]);

        Assert.True(GroupOrderRequestMapper.LinesBelongToOrder(request, StoredOrder()));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public void LinesBelongToOrder_AnotherOrdersLine_IsFalse(int lineId)
    {
        Assert.False(GroupOrderRequestMapper.LinesBelongToOrder(CreateRequest(students: [Student(lineId, 20)]),
            StoredOrder()));
    }

    //ერთი ხაზი ორ ხაზად ვერ შეინახება
    [Fact]
    public void LinesBelongToOrder_TheSameLineTwice_IsFalse()
    {
        Assert.False(GroupOrderRequestMapper.LinesBelongToOrder(
            CreateRequest(students: [Student(201, 20), Student(201, 21)]), StoredOrder()));
    }

    [Fact]
    public void LinesBelongToOrder_AnotherOrdersScheduleLine_IsFalse()
    {
        Assert.False(GroupOrderRequestMapper.LinesBelongToOrder(CreateRequest(dayTimePlaces: [DayTimePlace(302)]),
            StoredOrder()));
    }

    //ახალ ხაზებს თითო ტიპში -1, -2, … ეძლევა, არსებული Id-ს ინარჩუნებს
    [Fact]
    public void ToData_NewLines_GetTemporaryIdsByTheirPosition()
    {
        GroupOrderRequest request = CreateRequest(
            [new GroupOrderTeacherRequest { TeacherContractId = 5, SalarySchemaId = 8 }],
            [Student(0, 20), Student(202, 21), Student(0, 22)], [DayTimePlace(0), DayTimePlace(0)]);

        GroupOrderData data = GroupOrderRequestMapper.ToData(request, 7, 3, NoDefaultSchemes);

        Assert.Equal([-1], data.Teachers.Select(t => t.Id));
        Assert.Equal([-1, 202, -3], data.Students.Select(s => s.Id));
        Assert.Equal([-1, -2], data.DayTimePlaces.Select(d => d.Id));
    }

    [Fact]
    public void ToData_CreateOrder_HasTheGroupFieldsWithTrimmedCode()
    {
        GroupOrderRequest template = CreateRequest();
        var request = new GroupOrderRequest
        {
            OrderType = template.OrderType,
            OrderDate = new DateTime(2026, 12, 1, 15, 30, 0, DateTimeKind.Unspecified),
            AcademicYearId = 11,
            GroupCode = " 601 ",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            Teachers = template.Teachers,
            Students = template.Students,
            DayTimePlaces = template.DayTimePlaces
        };

        GroupOrderData data = GroupOrderRequestMapper.ToData(request, int.MaxValue, 0, NoDefaultSchemes);

        Assert.Equal(int.MaxValue, data.Id);
        Assert.Equal(EGroupOrderType.CreateGroup, data.OrderType);
        Assert.Equal(Day(12, 1), data.OrderDate);
        Assert.Equal(0, data.GroupId);
        Assert.False(data.IsPaused);
        Assert.Equal(new GroupFields(11, "601", 6, 2, 10), data.Fields);
    }

    //D187: სქემის გარეშე მოსული მასწავლებელი კონტრაქტის ძირითად სქემას იღებს
    [Fact]
    public void ToData_TeacherWithoutScheme_GetsTheContractsDefaultScheme()
    {
        GroupOrderRequest request = CreateRequest([new GroupOrderTeacherRequest { TeacherContractId = 5 }]);

        GroupOrderData data =
            GroupOrderRequestMapper.ToData(request, 7, 3, new Dictionary<int, int> { [5] = 9 });

        Assert.Equal(9, Assert.Single(data.Teachers).SalarySchemaId);
    }

    [Theory]
    [InlineData(" შენიშვნა ", "შენიშვნა")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ToData_StudentNote_IsTrimmedAndEmptyIsNull(string? note, string? expected)
    {
        GroupOrderData data = GroupOrderRequestMapper.ToData(CreateRequest(students: [Student(0, 20, note)]), 7, 3,
            NoDefaultSchemes);

        Assert.Equal(expected, Assert.Single(data.Students).Note);
    }

    //საწყისი ბრძანება: ხაზებს თარიღები და საათები აქვს, ჯგუფს გაუქმების თარიღი
    [Fact]
    public void ToData_InitialOrder_KeepsLineDatesHoursAndVoidDate()
    {
        var request = new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.Initial,
            OrderDate = Day(9, 1),
            AcademicYearId = 11,
            GroupCode = "601",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            VoidDate = new DateTime(2027, 6, 1, 12, 0, 0, DateTimeKind.Unspecified),
            Students =
            [
                new GroupOrderStudentRequest
                {
                    Id = 201,
                    StudentContractId = 20,
                    FourWeekHours = 8,
                    FourWeekFee = 48,
                    OneHourFee = 6,
                    HoursCoefficient = 1,
                    StartDate = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Unspecified),
                    EndDate = Day(11, 1)
                }
            ]
        };

        GroupOrderData data = GroupOrderRequestMapper.ToData(request, 1, 3, NoDefaultSchemes);

        Assert.Equal(new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Unspecified), data.VoidDate);
        Assert.Equal(new GroupOrderStudentLine(201, 20, 8f, 48m, 6m, 1f, null, Day(9, 1), Day(11, 1)),
            Assert.Single(data.Students));
    }

    [Fact]
    public void ToData_StudentsOrder_HasNoGroupFieldsAndKeepsTheTarget()
    {
        var request = new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.TransferStudents,
            OrderDate = Day(12, 1),
            GroupId = 3,
            TargetGroupId = 4,
            Students = [Student(0, 20)]
        };

        GroupOrderData data = GroupOrderRequestMapper.ToData(request, 7, 3, NoDefaultSchemes);

        Assert.Null(data.Fields);
        Assert.Equal(3, data.GroupId);
        Assert.Equal(4, data.TargetGroupId);
    }
}
