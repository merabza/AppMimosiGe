using System;
using AppMimosiGe.Application.GroupOrders.Models;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ბრძანებების ძრავის ტესტების სინთეზური ჯგუფი და მისი სტრიქონები
internal static class GroupOrdersTestData
{
    public static readonly GroupFields Fields = new(11, "601", 6, 2, 10);

    public static DateTime Day(int month, int day)
    {
        return new DateTime(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    public static GroupTeacherRow TeacherRow(DateTime startDate, DateTime? endDate = null, int lineId = 1)
    {
        return new GroupTeacherRow(lineId, 5, 8, startDate, endDate);
    }

    public static GroupStudentRow StudentRow(DateTime startDate, DateTime? endDate = null, int lineId = 2)
    {
        return new GroupStudentRow(lineId, 20, 8f, 48m, 6m, 1f, null, startDate, endDate);
    }

    public static GroupDayTimePlaceRow DayTimePlaceRow(DateTime startDate, DateTime? endDate = null, int lineId = 3)
    {
        return new GroupDayTimePlaceRow(lineId, 1, 4, 2f, 1, startDate, endDate);
    }

    //ჯგუფი, რომლის სტრიქონები, თუ სხვა არ არის მითითებული, 01.09-იდან მოქმედებს
    public static GroupState State(DateTime? voidDate = null, GroupTeacherRow[]? teachers = null,
        GroupStudentRow[]? students = null, GroupDayTimePlaceRow[]? dayTimePlaces = null)
    {
        return new GroupState(Fields, voidDate, teachers ?? [TeacherRow(Day(9, 1))],
            students ?? [StudentRow(Day(9, 1))], dayTimePlaces ?? [DayTimePlaceRow(Day(9, 1))]);
    }

    //ბრძანება ხაზების გარეშე
    public static GroupOrderData Order(int id, EGroupOrderType orderType, DateTime orderDate, int groupId)
    {
        return new GroupOrderData { Id = id, OrderType = orderType, OrderDate = orderDate, GroupId = groupId };
    }

    //საწყისი ბრძანება: ჯგუფი 01.09-იდან, მასწავლებლით 5 (სქემა 8), განრიგით და მოსწავლეებით students
    public static GroupOrderData InitialOrder(int id, int groupId, params GroupOrderStudentLine[] students)
    {
        return Order(id, EGroupOrderType.Initial, Day(9, 1), groupId) with
        {
            Fields = Fields,
            Teachers = [new GroupOrderTeacherLine(groupId * 10 + 1, 5, 8, Day(9, 1))],
            Students = students,
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(groupId * 10 + 2, 1, 4, 2f, 1, Day(9, 1))]
        };
    }

    //მოსწავლის ხაზი: კოეფიციენტი 1, 4 კვირაში 8 საათი, საათის ფასი oneHourFee. საათები მხოლოდ საწყის ბრძანებაში
    //მოდის ხაზიდან; სხვა ბრძანებაში მათ ფორმულა იძლევა (D187), 2-საათიანი განრიგით იგივე 8-ს
    public static GroupOrderStudentLine StudentLine(int id, int studentContractId, decimal oneHourFee = 6m,
        string? note = null)
    {
        return new GroupOrderStudentLine(id, studentContractId, 8f, 8 * oneHourFee, oneHourFee, 1f, note);
    }

    //საწყისი ბრძანების მოსწავლის ხაზი თარიღებით
    public static GroupOrderStudentLine InitialStudentLine(int id, int studentContractId, DateTime? endDate = null)
    {
        return StudentLine(id, studentContractId) with { StartDate = Day(9, 1), EndDate = endDate };
    }

    //სტრიქონი, რომელსაც StudentLine-ის ხაზი ქმნის
    public static GroupStudentRow StudentLineRow(int lineId, int studentContractId, DateTime startDate,
        DateTime? endDate, decimal oneHourFee = 6m, string? note = null)
    {
        return new GroupStudentRow(lineId, studentContractId, 8f, 8 * oneHourFee, oneHourFee, 1f, note, startDate,
            endDate);
    }

    //მოსწავლეების ბრძანება (3, 4, 5, 8)
    public static GroupOrderData StudentsOrder(int id, EGroupOrderType orderType, DateTime orderDate, int groupId,
        params GroupOrderStudentLine[] students)
    {
        return Order(id, orderType, orderDate, groupId) with { Students = students };
    }

    //გაყვანის (4) ხაზი: ტარიფის გარეშე
    public static GroupOrderStudentLine RemovalLine(int id, int studentContractId, string? note = null)
    {
        return new GroupOrderStudentLine(id, studentContractId, null, null, null, null, note);
    }
}
