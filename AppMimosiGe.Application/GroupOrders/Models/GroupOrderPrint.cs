using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების ბეჭდვის მონაცემი (D193, GROUP_ORDERS.md §7). ძველ მნიშვნელობებს ცვლილების ბრძანებები აჩვენებს:
///     მასწავლებელს 2 და 9, განრიგს 6 და 10, მოსწავლის ტარიფს 8 და 10. ისინი ჯგუფის მდგომარეობიდან მოდის ბრძანებამდე
///     (GroupStatesBeforeOrder), ბრძანების თარიღზე. მოსწავლეები სახელით ლაგდება, განრიგი კვირის დღითა და დაწყების დროით
/// </summary>
public static class GroupOrderPrint
{
    /// <summary>
    ///     ბრძანების ბეჭდვას ჯგუფის მდგომარეობა სჭირდება ბრძანებამდე
    /// </summary>
    public static bool ShowsPreviousState(EGroupOrderType orderType)
    {
        return ShowsPreviousTeacher(orderType) || ShowsPreviousSchedule(orderType) ||
               ShowsPreviousTariffs(orderType);
    }

    /// <summary>
    ///     მასწავლებლების კონტრაქტები, რომელთა სახელიც ბეჭდვას სჭირდება: ბრძანების მასწავლებელი და ძველი მასწავლებელი
    /// </summary>
    public static IReadOnlyCollection<int> TeacherContractIds(GroupOrderData order, GroupState? previous)
    {
        IEnumerable<int> previousTeacher = PreviousTeacher(order, previous) is { } teacher
            ? [teacher.TeacherContractId]
            : [];
        return [.. order.Teachers.Select(t => t.TeacherContractId).Concat(previousTeacher).Distinct()];
    }

    /// <summary>
    ///     მოსწავლეების კონტრაქტები, რომელთა სახელიც ბეჭდვას სჭირდება
    /// </summary>
    public static IReadOnlyCollection<int> StudentContractIds(GroupOrderData order)
    {
        return [.. order.Students.Select(s => s.StudentContractId).Distinct()];
    }

    /// <summary>
    ///     previous: ჯგუფის მდგომარეობა ბრძანებამდე, თუ ShowsPreviousState; სხვაგვარად null. საწყის ბრძანებას ნომერი არ
    ///     აქვს და ის არ იბეჭდება (D193)
    /// </summary>
    public static GroupOrderPrintResponse Build(GroupOrderPrintSource source, GroupState? previous,
        GroupOrderPrintNames names)
    {
        GroupOrderData order = source.Order;
        OrderNumber number = source.Number ??
                             throw new InvalidOperationException($"Group order {order.Id} has no number");
        DateTime issueDate = source.IssueDate ??
                             throw new InvalidOperationException($"Group order {order.Id} has no issue date");
        List<GroupOrderPrintDayTimePlaceResponse> previousDayTimePlaces =
            ShowsPreviousSchedule(order.OrderType) && previous is not null
                ? DayTimePlaces(
                    previous.DayTimePlaces.Where(d => GroupActivity.IsRowActiveOn(d.StartDate, d.EndDate,
                        order.OrderDate)).Select(d => (d.WeekDayId, d.LessonStartTimeId, d.HoursCount, d.RoomId)),
                    names)
                : [];

        return new GroupOrderPrintResponse(order.Id, (int)order.OrderType, number.ToString(), issueDate,
            order.OrderDate, source.OrganizationName ?? string.Empty, source.SignerTitle ?? string.Empty,
            source.SignerName ?? string.Empty, source.GroupCode, source.TargetGroupCode, source.AcademicYearName,
            source.CourseName, source.GroupSizeName, source.StudentStatusName,
            order.Teachers is [var line, ..] ? Teacher(line.TeacherContractId, line.SalarySchemaId, names) : null,
            PreviousTeacher(order, previous) is { } row ? Teacher(row.TeacherContractId, row.SalarySchemaId, names) : null,
            DayTimePlaces(order.DayTimePlaces.Select(d => (d.WeekDayId, d.LessonStartTimeId, d.HoursCount, d.RoomId)),
                names), previousDayTimePlaces, Students(order, previous, names));
    }

    private static bool ShowsPreviousTeacher(EGroupOrderType orderType)
    {
        return orderType is EGroupOrderType.ChangeTeacher or EGroupOrderType.ChangeSalaryScheme;
    }

    private static bool ShowsPreviousSchedule(EGroupOrderType orderType)
    {
        return orderType is EGroupOrderType.ChangeSchedule or EGroupOrderType.ChangeWeeklyHours;
    }

    private static bool ShowsPreviousTariffs(EGroupOrderType orderType)
    {
        return orderType is EGroupOrderType.ChangeStudentFees or EGroupOrderType.ChangeWeeklyHours;
    }

    //მასწავლებელი, რომელსაც ბრძანება ცვლის (2) ან რომელსაც სქემას უცვლის (9): ბრძანების თარიღზე ერთი მასწავლებელია
    //(D55); 2-ში შეიძლება არც იყოს
    private static GroupTeacherRow? PreviousTeacher(GroupOrderData order, GroupState? previous)
    {
        return ShowsPreviousTeacher(order.OrderType)
            ? previous?.Teachers.FirstOrDefault(t =>
                GroupActivity.IsRowActiveOn(t.StartDate, t.EndDate, order.OrderDate))
            : null;
    }

    private static GroupOrderPrintTeacherResponse Teacher(int teacherContractId, int salarySchemaId,
        GroupOrderPrintNames names)
    {
        return new GroupOrderPrintTeacherResponse(names.Teachers[teacherContractId],
            names.SalarySchemes[salarySchemaId]);
    }

    private static List<GroupOrderPrintDayTimePlaceResponse> DayTimePlaces(
        IEnumerable<(int WeekDayId, int LessonStartTimeId, float HoursCount, int RoomId)> dayTimePlaces,
        GroupOrderPrintNames names)
    {
        return
        [
            .. dayTimePlaces.OrderBy(d => d.WeekDayId)
                .ThenBy(d => names.LessonStartTimes[d.LessonStartTimeId], StringComparer.Ordinal).Select(d =>
                    new GroupOrderPrintDayTimePlaceResponse(names.WeekDays[d.WeekDayId],
                        names.LessonStartTimes[d.LessonStartTimeId], d.HoursCount, names.Rooms[d.RoomId]))
        ];
    }

    //ძველი ტარიფი (8, 10): მოსწავლის სტრიქონი, რომელიც ბრძანების თარიღზე მოქმედებდა
    private static List<GroupOrderPrintStudentResponse> Students(GroupOrderData order, GroupState? previous,
        GroupOrderPrintNames names)
    {
        bool showsPreviousTariffs = ShowsPreviousTariffs(order.OrderType);
        return
        [
            .. order.Students.Select(line =>
            {
                GroupStudentRow? previousRow = showsPreviousTariffs
                    ? previous?.Students.FirstOrDefault(s =>
                        s.StudentContractId == line.StudentContractId &&
                        GroupActivity.IsRowActiveOn(s.StartDate, s.EndDate, order.OrderDate))
                    : null;
                return new GroupOrderPrintStudentResponse(names.Students[line.StudentContractId],
                    line.FourWeekHours, line.OneHourFee, line.FourWeekFee, line.Note, previousRow?.FourWeekHours,
                    previousRow?.OneHourFee, previousRow?.FourWeekFee);
            }).OrderBy(s => s.StudentName, StringComparer.Ordinal)
        ];
    }
}
