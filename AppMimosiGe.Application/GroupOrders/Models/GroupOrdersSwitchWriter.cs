using System;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდების ჩაწერა ბაზის ჯგუფებში. სტრიქონების ველები და თარიღები არ იცვლება, იცვლება მხოლოდ მათი კავშირი
///     ბრძანების ხაზთან: ვიზარდი 1 მას ქმნის (D194), ვიზარდი 2 შლის (D195)
/// </summary>
public static class GroupOrdersSwitchWriter
{
    /// <summary>
    ///     ჯგუფის საწყისი ბრძანება ბაზისთვის (D196) და ჯგუფის ყოველი სტრიქონის კავშირი თავის ხაზთან. ბრძანება 0-ის ხაზი
    ///     სტრიქონს 1:1 იმეორებს და მის Id-ს ატარებს (GroupOrdersSwitchPlanner.InitialOrder), ამიტომ სტრიქონი ახალ ხაზს ამ
    ///     Id-ით პოულობს და მას navigation-ით უკავშირდება: ხაზის Id-ს SaveChanges ჩაწერს. ნომერი, გამოცემა და ბეჭდვის
    ///     მონაცემები საწყის ბრძანებას არ აქვს (D192, D193)
    /// </summary>
    public static GroupOrder CreateInitialOrder(Group group, GroupOrderData initialOrder, string userName,
        DateTime now)
    {
        var order = new GroupOrder
        {
            CreatedByUserName = userName, CreatedAt = now, AcademicYearId = initialOrder.Fields!.AcademicYearId
        };
        //საწყისი ბრძანების საათები ხაზიდან მოდის, ამიტომ განრიგი აქ არ სჭირდება
        (GroupOrderNewLines lines, _) = GroupOrderEntityMapper.Write(order, initialOrder, []);
        foreach (GroupByTeacher row in group.GroupsByTeachers)
        {
            row.GroupOrderTeacher = lines.Teachers[row.Id];
        }

        foreach (GroupByStudent row in group.GroupsByStudents)
        {
            row.GroupOrderStudent = lines.Students[row.GbsId];
        }

        foreach (GroupDayTimePlace row in group.GroupDayTimePlaces)
        {
            row.GroupOrderDayTimePlace = lines.DayTimePlaces[row.GdtpId];
        }

        return order;
    }

    /// <summary>
    ///     სტრიქონების კავშირის მოხსნა ხაზებთან. ჯერ კავშირი იშლება, მერე ბრძანებები: სტრიქონი ხაზს Restrict-ით ებმის
    /// </summary>
    public static void Unlink(GroupOrderLinkedRows rows)
    {
        foreach (GroupByTeacher row in rows.Teachers)
        {
            row.GroupOrderTeacherId = null;
            row.GroupOrderTeacher = null;
        }

        foreach (GroupByStudent row in rows.Students)
        {
            row.GroupOrderStudentId = null;
            row.GroupOrderStudent = null;
        }

        foreach (GroupDayTimePlace row in rows.DayTimePlaces)
        {
            row.GroupOrderDayTimePlaceId = null;
            row.GroupOrderDayTimePlace = null;
        }
    }
}
