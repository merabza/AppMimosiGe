using System;

namespace AppMimosiGe.Infrastructure.Repositories;

//ნამუშევარი დროის სიის სტრიქონი SQL-ში, რომ დალაგება და გვერდებად დაყოფა სერვერზე მოხდეს (EF პროექციას
//member-init-ით თარგმნის)
internal sealed class WorkHourRowData
{
    public int Id { get; init; }
    public int TeacherContractId { get; init; }
    public required string EmployeeName { get; init; }
    public DateTime WhStart { get; init; }
    public DateTime? WhEnd { get; init; }
}
