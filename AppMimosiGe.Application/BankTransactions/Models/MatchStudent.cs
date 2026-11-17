namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     მოსწავლე, რომელსაც მიმდინარე სასწავლო წლის კონტრაქტი აქვს (ჩარიცხვის კანდიდატი)
/// </summary>
public sealed record MatchStudent(int StudentHumanId, string FirstName, string LastName);
