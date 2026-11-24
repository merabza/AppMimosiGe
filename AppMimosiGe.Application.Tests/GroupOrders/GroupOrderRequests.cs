using System;
using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ბრძანებების მოთხოვნები handler-ების ტესტებისთვის. მოსწავლის ტარიფი 2-საათიანი კვირის განრიგისაა: 4 × 1 × 2 = 8 საათი
internal static class GroupOrderRequests
{
    public static GroupOrderStudentRequest Student(int studentContractId, decimal oneHourFee = 6m, int id = 0,
        string? note = null)
    {
        return new GroupOrderStudentRequest
        {
            Id = id,
            StudentContractId = studentContractId,
            FourWeekFee = 8 * oneHourFee,
            OneHourFee = oneHourFee,
            HoursCoefficient = 1,
            Note = note
        };
    }

    public static GroupOrderStudentRequest Removal(int studentContractId, int id = 0)
    {
        return new GroupOrderStudentRequest { Id = id, StudentContractId = studentContractId };
    }

    public static GroupOrderTeacherRequest Teacher(int teacherContractId = 5, int? salarySchemaId = 8, int id = 0)
    {
        return new GroupOrderTeacherRequest
        {
            Id = id, TeacherContractId = teacherContractId, SalarySchemaId = salarySchemaId
        };
    }

    //ორშაბათი (ან weekDayId) 15:00, 2 საათი, ოთახი 1
    public static GroupOrderDayTimePlaceRequest Monday(int id = 0, int weekDayId = 1)
    {
        return new GroupOrderDayTimePlaceRequest
        {
            Id = id, WeekDayId = weekDayId, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1
        };
    }

    public static GroupOrderRequest StudentsRequest(EGroupOrderType orderType, int groupId, DateTime orderDate,
        params GroupOrderStudentRequest[] students)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)orderType, OrderDate = orderDate, GroupId = groupId, Students = [.. students]
        };
    }

    public static GroupOrderRequest TransferRequest(int groupId, int targetGroupId, DateTime orderDate,
        params GroupOrderStudentRequest[] students)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.TransferStudents,
            OrderDate = orderDate,
            GroupId = groupId,
            TargetGroupId = targetGroupId,
            Students = [.. students]
        };
    }

    public static GroupOrderRequest TeacherRequest(EGroupOrderType orderType, int groupId, DateTime orderDate,
        GroupOrderTeacherRequest teacher)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)orderType, OrderDate = orderDate, GroupId = groupId, Teachers = [teacher]
        };
    }

    public static GroupOrderRequest CloseRequest(int groupId, DateTime orderDate)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.CloseGroup, OrderDate = orderDate, GroupId = groupId
        };
    }

    //შექმნის ბრძანება: წელი 11, საგანი 6, ზომა 2, სტატუსი 10
    public static GroupOrderRequest CreateGroupRequest(DateTime orderDate, List<GroupOrderTeacherRequest> teachers,
        List<GroupOrderStudentRequest> students, List<GroupOrderDayTimePlaceRequest> dayTimePlaces,
        string groupCode = "605", int academicYearId = 11)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.CreateGroup,
            OrderDate = orderDate,
            AcademicYearId = academicYearId,
            GroupCode = groupCode,
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            Teachers = teachers,
            Students = students,
            DayTimePlaces = dayTimePlaces
        };
    }
}
