namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     ჩატარებული გაკვეთილების ხელფასი (მდგენელი ტიპი 1)
/// </summary>
public sealed record CalculatedSalaryPart(int TeacherContractId, decimal Amount);
