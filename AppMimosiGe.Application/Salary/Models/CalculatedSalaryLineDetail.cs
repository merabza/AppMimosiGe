namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     სტრიქონის დეტალი: ჯგუფის თანხა, საათები და ერთი საათის ღირებულება
/// </summary>
public sealed record CalculatedSalaryLineDetail(int GroupId, decimal Amount, float HoursCount, decimal HourCost);
