using System.Collections.Generic;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     მოსწავლე, რომელსაც მიმდინარე სასწავლო წლის კონტრაქტი აქვს (ჩარიცხვის კანდიდატი)
/// </summary>
public sealed record MatchStudent(int StudentHumanId, string FirstName, string LastName);

/// <summary>
///     მიმდინარე წლის კონტრაქტების გადამხდელი (StudentContracts.PayerHumanId) და მისი მოსწავლეები
/// </summary>
public sealed record MatchPayer(
    int PayerHumanId,
    string PersonalId,
    string FirstName,
    string LastName,
    IReadOnlyList<int> StudentHumanIds);

/// <summary>
///     დასწავლილი გადამხდელი (StudentPayers, D162, D169): ჩარიცხვის გადამხდელი, რომელიც მენეჯერმა ხელით გაანაწილა
/// </summary>
public sealed record LearnedPayer(int StudentHumanId, string? PersonalNumber, string? AccountIban);

/// <param name="Students">მიმდინარე წლის მოსწავლეები</param>
/// <param name="StudentByContractNumber">ყველა წლის კონტრაქტის ნომერი → მოსწავლე</param>
/// <param name="Payers">მიმდინარე წლის კონტრაქტების გადამხდელები</param>
/// <param name="LearnedPayers">დასწავლილი გადამხდელები</param>
public sealed record PaymentMatchInput(
    IReadOnlyList<MatchStudent> Students,
    IReadOnlyDictionary<string, int> StudentByContractNumber,
    IReadOnlyList<MatchPayer> Payers,
    IReadOnlyList<LearnedPayer> LearnedPayers);
