namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ჩარიცხვის ერთი წილი: მოსწავლე, მისი მიმდინარე წლის კონტრაქტი (გადახდა მასზე იწერება) და თანხა
/// </summary>
public sealed record PlannedShare(int StudentHumanId, int StudentContractId, decimal Amount);
