using System;

namespace AppMimosiGe.Infrastructure.Repositories;

//ჯგუფების სიის სტრიქონი SQL-ში: ძებნის სამივე რეჟიმი ამ ფორმამდე დაიყვანება, რომ დალაგება და გვერდებად დაყოფა
//ერთნაირად და სერვერზე მოხდეს (EF პროექციას member-init-ით თარგმნის). ზომა და სტატუსი ადგილების რაოდენობით და
//Rate-ით ლაგდება, როგორც Access-ის ჩამოსაშლელ სიებში
internal sealed class GroupRowData
{
    public int RowId { get; init; }
    public int GrpId { get; init; }
    public required string GroupCode { get; init; }
    public required string AcademicYearName { get; init; }
    public required string CourseName { get; init; }
    public int GroupSize { get; init; }
    public required string GroupSizeName { get; init; }
    public int StudentStatusRate { get; init; }
    public required string StudentStatusName { get; init; }
    public DateTime? VoidDate { get; init; }
    public bool DirtyLessons { get; init; }
    public string? TeacherName { get; init; }
    public int? ActiveStudentsCount { get; init; }
    public string? StudentName { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}
