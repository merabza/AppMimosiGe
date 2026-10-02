namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     ამონაწერის სტრიქონი: ოპერაცია და ნაშთი მის შემდეგ
/// </summary>
public sealed record StatementRow(BalanceOperation Operation, decimal RunningTotal);
