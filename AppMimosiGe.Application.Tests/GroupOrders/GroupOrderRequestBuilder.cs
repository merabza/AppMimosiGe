using System;
using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Requests;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//GroupOrderRequest-ის init-ველები ტესტისთვის: მოთხოვნის ასლი შეცვლილი ნაწილით
internal sealed class GroupOrderRequestBuilder(GroupOrderRequest request)
{
    public int OrderType { get; set; } = request.OrderType;
    public DateTime OrderDate { get; set; } = request.OrderDate;
    public int? GroupId { get; set; } = request.GroupId;
    public int? TargetGroupId { get; set; } = request.TargetGroupId;
    public int? AcademicYearId { get; set; } = request.AcademicYearId;
    public string? GroupCode { get; set; } = request.GroupCode;
    public int? CourseId { get; set; } = request.CourseId;
    public int? GroupSizeId { get; set; } = request.GroupSizeId;
    public int? StudentStatusId { get; set; } = request.StudentStatusId;
    public DateTime? VoidDate { get; set; } = request.VoidDate;
    public List<GroupOrderTeacherRequest> Teachers { get; set; } = request.Teachers;
    public List<GroupOrderStudentRequest> Students { get; set; } = request.Students;
    public List<GroupOrderDayTimePlaceRequest> DayTimePlaces { get; set; } = request.DayTimePlaces;

    public GroupOrderRequest Build()
    {
        return new GroupOrderRequest
        {
            OrderType = OrderType,
            OrderDate = OrderDate,
            GroupId = GroupId,
            TargetGroupId = TargetGroupId,
            AcademicYearId = AcademicYearId,
            GroupCode = GroupCode,
            CourseId = CourseId,
            GroupSizeId = GroupSizeId,
            StudentStatusId = StudentStatusId,
            VoidDate = VoidDate,
            Teachers = Teachers,
            Students = Students,
            DayTimePlaces = DayTimePlaces
        };
    }
}
