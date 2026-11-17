using System.Collections.Generic;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ერთი ახალი ჩარიცხვა: რომელმა წესმა იპოვა მოსწავლე(ები) და წილები (ცარიელი = გაუნაწილებელი)
/// </summary>
public sealed record PlannedTransaction(
    BankMovement Movement,
    EPaymentMatchStep Step,
    IReadOnlyList<PlannedShare> Shares);
