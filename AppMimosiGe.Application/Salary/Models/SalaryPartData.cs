namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     უწყისის მდგენელი, რომელსაც გამოთვლა ჯამავს (ხელით შეტანილი, ან გამოთვლით შექმნილი ტიპი 1)
/// </summary>
public sealed record SalaryPartData(int TeacherContractId, int? SalaryPartTypeId, decimal Amount);
