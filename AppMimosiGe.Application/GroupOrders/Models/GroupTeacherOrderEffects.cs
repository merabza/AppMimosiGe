using System;
using System.Collections.Generic;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     მასწავლებლის ბრძანებების ეფექტები: მასწავლებლის შეცვლა (2) და ხელფასის სქემის შეცვლა (9). ბრძანებას ერთი
///     მასწავლებლის ხაზი აქვს, ჯგუფს კი ერთ დღეს ერთი მასწავლებელი ჰყავს (D55), ამიტომ a-ზე მოქმედი სტრიქონი ერთია
/// </summary>
public static class GroupTeacherOrderEffects
{
    /// <summary>
    ///     2 (D190, D204): a-ზე მოქმედი მასწავლებლის სტრიქონი a-თი სრულდება და ახალი მასწავლებელი a-დან ძველის
    ///     დასრულებამდე იწყება. თუ მოქმედი მასწავლებელი არ არის, ახალი უბრალოდ ინიშნება, დასრულების გარეშე. ახალი
    ///     მასწავლებელი მოქმედს არ უნდა ემთხვეოდეს
    /// </summary>
    public static Result<GroupState> ChangeTeacher(GroupState state, GroupOrderData order)
    {
        GroupOrderTeacherLine line = SingleLine(order);
        List<GroupTeacherRow> teachers = [.. state.Teachers];
        int index = ActiveRowIndex(teachers, order.OrderDate);
        if (index < 0)
        {
            teachers.Add(line.ToRow(order.OrderDate, null));
            return state with { Teachers = teachers };
        }

        if (teachers[index].TeacherContractId == line.TeacherContractId)
        {
            return GroupOrderErrors.TeacherIsAlreadyCurrent;
        }

        return Replace(state, teachers, index, line, order.OrderDate);
    }

    /// <summary>
    ///     9 (D189): a-ზე მოქმედი მასწავლებლის სტრიქონი a-თი სრულდება და იგივე მასწავლებელი a-დან ძველის დასრულებამდე
    ///     ახალი სქემით იწყება. ახალი სქემა მოქმედს არ უნდა ემთხვეოდეს
    /// </summary>
    public static Result<GroupState> ChangeSalaryScheme(GroupState state, GroupOrderData order)
    {
        GroupOrderTeacherLine line = SingleLine(order);
        List<GroupTeacherRow> teachers = [.. state.Teachers];
        int index = ActiveRowIndex(teachers, order.OrderDate);
        if (index < 0 || teachers[index].TeacherContractId != line.TeacherContractId)
        {
            return GroupOrderErrors.TeacherIsNotCurrent;
        }

        if (teachers[index].SalarySchemaId == line.SalarySchemaId)
        {
            return GroupOrderErrors.SalarySchemeIsAlreadyCurrent;
        }

        return Replace(state, teachers, index, line, order.OrderDate);
    }

    //მოქმედი სტრიქონი a-თი სრულდება, ხაზის სტრიქონი a-დან მის დასრულებამდე იწყება. a-ზე დაწყებული სტრიქონი a-თი ვერ
    //დასრულდება (D206)
    private static Result<GroupState> Replace(GroupState state, List<GroupTeacherRow> teachers, int index,
        GroupOrderTeacherLine line, DateTime orderDate)
    {
        GroupTeacherRow current = teachers[index];
        if (current.StartDate.Date == orderDate.Date)
        {
            return GroupOrderErrors.RowStartsOnOrderDate;
        }

        teachers[index] = current with { EndDate = orderDate };
        teachers.Add(line.ToRow(orderDate, current.EndDate));
        return state with { Teachers = teachers };
    }

    //day დღეს მოქმედი სტრიქონი, ან -1
    private static int ActiveRowIndex(List<GroupTeacherRow> teachers, DateTime day)
    {
        return teachers.FindIndex(t => GroupActivity.IsRowActiveOn(t.StartDate, t.EndDate, day));
    }

    private static GroupOrderTeacherLine SingleLine(GroupOrderData order)
    {
        return order.Teachers is [GroupOrderTeacherLine line]
            ? line
            : throw new InvalidOperationException($"Group order {order.Id} must have exactly one teacher line");
    }
}
