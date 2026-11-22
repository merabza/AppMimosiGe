namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის ველები: სასწავლო წელი, კოდი, საგანი, ზომა და მოსწავლის სტატუსი. მათ მხოლოდ საწყისი (0) და შექმნის (1)
///     ბრძანება წერს (D186, D196)
/// </summary>
public sealed record GroupFields(
    int AcademicYearId,
    string GroupCode,
    int CourseId,
    int GroupSizeId,
    int StudentStatusId);
