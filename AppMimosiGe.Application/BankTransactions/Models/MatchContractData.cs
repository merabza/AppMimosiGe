namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     მიმდინარე სასწავლო წლის კონტრაქტი ჩარიცხვის მოსწავლის საძებნად: მოსწავლე და გადამხდელი (Humans)
/// </summary>
public sealed record MatchContractData(
    int StudentContractId,
    int StudentHumanId,
    string StudentFirstName,
    string StudentLastName,
    int PayerHumanId,
    string PayerPersonalId,
    string PayerFirstName,
    string PayerLastName);
