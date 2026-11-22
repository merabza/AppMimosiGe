using System;
using AppMimosiGe.Application.GroupOrders.Models;

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
}
