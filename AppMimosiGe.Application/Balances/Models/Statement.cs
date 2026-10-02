using System.Collections.Generic;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     ამონაწერი: საწყისი და საბოლოო ნაშთი და ფილტრის შუალედის სტრიქონები
/// </summary>
public sealed record Statement(decimal StartBalance, decimal EndBalance, List<StatementRow> Rows);
