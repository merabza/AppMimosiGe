using System.Collections.Generic;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     მიმდინარე წლის კონტრაქტების გადამხდელი (StudentContracts.PayerHumanId) და მისი მოსწავლეები
/// </summary>
public sealed record MatchPayer(
    int PayerHumanId,
    string PersonalId,
    string FirstName,
    string LastName,
    IReadOnlyList<int> StudentHumanIds);
