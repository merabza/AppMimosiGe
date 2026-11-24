namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების სიის დალაგების ველები. ნომერი რიცხვებით ლაგდება: ჯერ წელი, მერე რიგითი ნომერი (D192)
/// </summary>
public enum EGroupOrderSortField
{
    Number,
    OrderType,
    IssueDate,
    OrderDate,
    IsPaused,
    AcademicYearName,
    GroupCode,
    TargetGroupCode
}
