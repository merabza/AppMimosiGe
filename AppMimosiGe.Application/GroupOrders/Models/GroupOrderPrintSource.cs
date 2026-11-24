using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანება ბეჭდვისთვის (IGroupOrdersRepository.GetPrintSource): ბრძანება ხაზებით, ნომერი და გამოცემის მონაცემები
///     (D192, D193), ჯგუფის და სამიზნე ჯგუფის კოდი და ჯგუფის წლის, საგნის, ზომისა და სტატუსის სახელები. ნომერი და
///     გამოცემის თარიღი საწყის ბრძანებას არ აქვს. GroupSizeName: „ადგილები-დასახელება", როგორც ჯგუფის ფორმაში
/// </summary>
public sealed record GroupOrderPrintSource(
    GroupOrderData Order,
    OrderNumber? Number,
    DateTime? IssueDate,
    string? OrganizationName,
    string? SignerTitle,
    string? SignerName,
    string GroupCode,
    string? TargetGroupCode,
    string AcademicYearName,
    string CourseName,
    string GroupSizeName,
    string StudentStatusName);
