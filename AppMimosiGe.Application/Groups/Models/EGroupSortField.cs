namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფების სიის დალაგების ველები. ზოგი მხოლოდ ძებნის ერთ რეჟიმში გამოიყენება (GroupsListQueryFactory)
/// </summary>
public enum EGroupSortField
{
    GroupCode,
    AcademicYearName,
    CourseName,
    GroupSizeName,
    StudentStatusName,
    VoidDate,
    DirtyLessons,
    TeacherName,
    ActiveStudentsCount,
    StudentName,
    StartDate,
    EndDate
}
