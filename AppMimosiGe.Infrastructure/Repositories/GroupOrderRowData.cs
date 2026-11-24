using System;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

//ბრძანებების სიის სტრიქონი SQL-ში, რომ დალაგება და გვერდებად დაყოფა სერვერზე მოხდეს (EF პროექციას member-init-ით
//თარგმნის). ნომერი წლითა და რიგითი ნომრით ლაგდება, ტექსტად კი მხოლოდ ჩატვირთვის შემდეგ იწერება. GroupCode: ჯგუფის
//კოდი, დაპაუზებულ შექმნის ბრძანებას კი, რომელსაც ჯგუფი ჯერ არ აქვს, საკუთარი
internal sealed class GroupOrderRowData
{
    public int Id { get; init; }
    public EGroupOrderType OrderType { get; init; }
    public int? NumberYear { get; init; }
    public int? Number { get; init; }
    public DateTime? IssueDate { get; init; }
    public DateTime OrderDate { get; init; }
    public bool IsPaused { get; init; }
    public int AcademicYearId { get; init; }
    public required string AcademicYearName { get; init; }
    public int? GroupId { get; init; }
    public string? GroupCode { get; init; }
    public int? TargetGroupId { get; init; }
    public string? TargetGroupCode { get; init; }
}
