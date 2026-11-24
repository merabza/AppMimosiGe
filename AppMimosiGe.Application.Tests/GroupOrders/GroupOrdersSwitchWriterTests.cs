using System;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersModeTestData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდების ჩაწერა: საწყისი ბრძანება და სტრიქონების კავშირი ხაზებთან (D194, D196), კავშირის მოხსნა (D195)
public sealed class GroupOrdersSwitchWriterTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 16, 30, 0, DateTimeKind.Unspecified);

    private static GroupOrder CreateInitialOrder(Group group)
    {
        GroupOrderData initialOrder =
            GroupOrdersSwitchPlanner.InitialOrder(group.GrpId, GroupStateMapper.ToState(group), Day(11, 24))!;
        return GroupOrdersSwitchWriter.CreateInitialOrder(group, initialOrder, "admin", Now);
    }

    //D196: ბრძანება 0 ჯგუფის ველებს, VoidDate-ს და ყველა სტრიქონს იმეორებს; ნომერი, გამოცემა და ბეჭდვის მონაცემები
    //არ აქვს (D192, D193)
    [Fact]
    public void CreateInitialOrder_RepeatsTheGroupWithoutNumberOrIssue()
    {
        GroupOrder order = CreateInitialOrder(StoredGroup(4, "605", voidDate: Day(12, 31)));

        Assert.Equal(
        (EGroupOrderType.Initial, Day(9, 1), false, (int?)4, (int?)null, 11, "605", (int?)6, (int?)2, (int?)10,
            (DateTime?)Day(12, 31)),
        (order.OrderType, order.OrderDate, order.IsPaused, order.GroupId, order.TargetGroupId, order.AcademicYearId,
            order.GroupCode, order.CourseId, order.GroupSizeId, order.StudentStatusId, order.VoidDate));
        Assert.Equal(("admin", Now, (string?)null, (DateTime?)null),
            (order.CreatedByUserName, order.CreatedAt, order.ChangedByUserName, order.ChangedAt));
        Assert.Equal(((int?)null, (int?)null, (DateTime?)null, (string?)null, (string?)null, (string?)null),
            (order.NumberYear, order.Number, order.IssueDate, order.OrganizationName, order.SignerTitle,
                order.SignerName));
    }

    //ყოველი სტრიქონი თავის ხაზს navigation-ით უკავშირდება; ხაზი სტრიქონს თარიღებითა და ტარიფით იმეორებს
    [Fact]
    public void CreateInitialOrder_LinksEveryRowToItsOwnLine()
    {
        Group group = StoredGroup(4, "605");

        GroupOrder order = CreateInitialOrder(group);

        GroupByTeacher teacher = Assert.Single(group.GroupsByTeachers);
        Assert.Same(Assert.Single(order.GroupOrderTeachers), teacher.GroupOrderTeacher);
        Assert.Equal((5, 8, (DateTime?)Day(9, 1), (DateTime?)null),
            (teacher.GroupOrderTeacher!.TeacherContractId, teacher.GroupOrderTeacher.SalarySchemaId,
                teacher.GroupOrderTeacher.StartDate, teacher.GroupOrderTeacher.EndDate));
        Assert.Equal(2, order.GroupOrderStudents.Count);
        Assert.All(group.GroupsByStudents, row =>
        {
            GroupOrderStudent line = row.GroupOrderStudent!;
            Assert.Contains(line, order.GroupOrderStudents);
            Assert.Equal(
                (row.StudentContractId, (float?)row.FourWeekHours, (decimal?)row.FourWeekFee,
                    (decimal?)row.OneHourFee, (float?)row.HoursCoefficient, row.Note, (DateTime?)row.StartDate,
                    row.EndDate),
                (line.StudentContractId, line.FourWeekHours, line.FourWeekFee, line.OneHourFee,
                    line.HoursCoefficient, line.Note, line.StartDate, line.EndDate));
        });
        GroupDayTimePlace dayTimePlace = Assert.Single(group.GroupDayTimePlaces);
        Assert.Same(Assert.Single(order.GroupOrderDayTimePlaces), dayTimePlace.GroupOrderDayTimePlace);
        Assert.Equal((1, 4, 2f, 1, (DateTime?)Day(9, 1)),
            (dayTimePlace.GroupOrderDayTimePlace!.WeekDayId, dayTimePlace.GroupOrderDayTimePlace.LessonStartTimeId,
                dayTimePlace.GroupOrderDayTimePlace.HoursCount, dayTimePlace.GroupOrderDayTimePlace.RoomId,
                dayTimePlace.GroupOrderDayTimePlace.StartDate));
    }

    //სტრიქონების ველები და თარიღები არ იცვლება: ჯგუფი ისეთივე რჩება
    [Fact]
    public void CreateInitialOrder_LeavesTheRowsUnchanged()
    {
        Group group = StoredGroup(4, "605");
        GroupState before = GroupStateMapper.ToState(group);

        CreateInitialOrder(group);

        Assert.Equivalent(before, GroupStateMapper.ToState(group), true);
        Assert.Equal([41], group.GroupsByTeachers.Select(t => t.Id));
        Assert.Equal([42, 43], group.GroupsByStudents.Select(s => s.GbsId));
        Assert.Equal([44], group.GroupDayTimePlaces.Select(d => d.GdtpId));
    }

    [Fact]
    public void Unlink_ClearsTheLinksOfEveryRow()
    {
        Group group = StoredGroup(4, "605");
        CreateInitialOrder(group);
        group.GroupsByTeachers.Single().GroupOrderTeacherId = 7;
        var rows = new GroupOrderLinkedRows([.. group.GroupsByTeachers], [.. group.GroupsByStudents],
            [.. group.GroupDayTimePlaces]);

        GroupOrdersSwitchWriter.Unlink(rows);

        Assert.All(group.GroupsByTeachers,
            r => Assert.True(r is { GroupOrderTeacherId: null, GroupOrderTeacher: null }));
        Assert.All(group.GroupsByStudents,
            r => Assert.True(r is { GroupOrderStudentId: null, GroupOrderStudent: null }));
        Assert.All(group.GroupDayTimePlaces,
            r => Assert.True(r is { GroupOrderDayTimePlaceId: null, GroupOrderDayTimePlace: null }));
    }
}
