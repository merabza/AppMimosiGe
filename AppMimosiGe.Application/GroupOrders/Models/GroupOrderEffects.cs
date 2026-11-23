using System;
using System.Linq;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების ტიპების ეფექტები ჯგუფის მდგომარეობაზე (GROUP_ORDERS.md §4). ჯგუფს ერთხელ ქმნის საწყისი (0) ან შექმნის
///     (1) ბრძანება: მანამდე სხვა ბრძანება არ ესადაგება, მის შემდეგ კი თვითონ ისინი აღარ ესადაგება. ახალი ტიპის ეფექტი
///     Apply-ის switch-ს ემატება. გადაყვანა (5) ორ ჯგუფს ცვლის, ამიტომ მისი ეფექტი ცალკეა
///     (GroupStudentOrderEffects.Transfer)
/// </summary>
public static class GroupOrderEffects
{
    /// <summary>
    ///     order-ის ეფექტი state-ზე (null: ჯგუფი ჯერ არ შექმნილა). აბრუნებს ახალ მდგომარეობას, ან მიზეზს, რის გამოც
    ///     ბრძანება მდგომარეობას არ ესადაგება
    /// </summary>
    public static Result<GroupState> Apply(GroupState? state, GroupOrderData order)
    {
        return (order.OrderType, state) switch
        {
            (EGroupOrderType.TransferStudents, _) => throw new ArgumentException(
                "A transfer changes two groups: use GroupStudentOrderEffects.Transfer", nameof(order)),
            (EGroupOrderType.Initial, null) => Initial(order),
            (EGroupOrderType.CreateGroup, null) => CreateGroup(order),
            (EGroupOrderType.Initial or EGroupOrderType.CreateGroup, _) => GroupOrderErrors.CreateOrderCannotHaveGroup,
            //ჯგუფის შექმნამდე, ანუ მის დაწყებამდე, სხვა ბრძანება არ ესადაგება (D190)
            (_, null) => GroupOrderErrors.OrderIsBeforeGroupStart,
            (EGroupOrderType.ChangeTeacher, { } created) => GroupTeacherOrderEffects.ChangeTeacher(created, order),
            (EGroupOrderType.AddStudents, { } created) => GroupStudentOrderEffects.AddStudents(created, order),
            (EGroupOrderType.RemoveStudents, { } created) => GroupStudentOrderEffects.RemoveStudents(created, order),
            (EGroupOrderType.CloseGroup, { } created) => CloseGroup(created, order),
            (EGroupOrderType.ChangeStudentFees, { } created) =>
                GroupStudentOrderEffects.ChangeStudentFees(created, order),
            (EGroupOrderType.ChangeSalaryScheme, { } created) =>
                GroupTeacherOrderEffects.ChangeSalaryScheme(created, order),
            //6 და 10 ტიპების ეფექტები გეგმის B3 ამოცანაშია
            _ => throw new NotSupportedException($"Group order type {order.OrderType} is not supported yet")
        };
    }

    //საწყისი ბრძანება (D196): ჯგუფის ველები, გაუქმების თარიღი და ყველა სტრიქონი თავისი თარიღებით, დასრულებულებიც
    private static GroupState Initial(GroupOrderData order)
    {
        return new GroupState(RequiredFields(order), order.VoidDate,
            [.. order.Teachers.Select(t => t.ToRow(RequiredStartDate(order, t.StartDate), t.EndDate))],
            [.. order.Students.Select(s => s.ToRow(RequiredStartDate(order, s.StartDate), s.EndDate))],
            [.. order.DayTimePlaces.Select(d => d.ToRow(RequiredStartDate(order, d.StartDate), d.EndDate))]);
    }

    //შექმნის ბრძანება (D186): ჯგუფის ველები და ყველა სტრიქონი ბრძანების თარიღიდან
    private static GroupState CreateGroup(GroupOrderData order)
    {
        return new GroupState(RequiredFields(order), null,
            [.. order.Teachers.Select(t => t.ToRow(order.OrderDate, null))],
            [.. order.Students.Select(s => s.ToRow(order.OrderDate, null))],
            [.. order.DayTimePlaces.Select(d => d.ToRow(order.OrderDate, null))]);
    }

    //დახურვის ბრძანება (D190): მხოლოდ VoidDate = a
    private static GroupState CloseGroup(GroupState state, GroupOrderData order)
    {
        return state with { VoidDate = order.OrderDate };
    }

    private static GroupFields RequiredFields(GroupOrderData order)
    {
        return order.Fields ?? throw new InvalidOperationException($"Group order {order.Id} has no group fields");
    }

    //საწყისი ბრძანების ხაზს თავისი სტრიქონის დაწყების თარიღი აქვს
    private static DateTime RequiredStartDate(GroupOrderData order, DateTime? startDate)
    {
        return startDate ??
               throw new InvalidOperationException($"Initial group order {order.Id} has a line without a start date");
    }
}
