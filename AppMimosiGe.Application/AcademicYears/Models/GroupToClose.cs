namespace AppMimosiGe.Application.AcademicYears.Models;

/// <summary>
///     დასახურავი ჯგუფი: ID, კოდი და საგნის სახელი (გენერატორი ჯგუფს საგნის გარეშე ტვირთავს)
/// </summary>
public sealed record GroupToClose(int GrpId, string GroupCode, string CourseName);
