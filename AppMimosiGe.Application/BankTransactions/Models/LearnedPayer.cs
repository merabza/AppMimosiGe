namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     დასწავლილი გადამხდელი (StudentPayers, D162, D169): ჩარიცხვის გადამხდელი, რომელიც მენეჯერმა ხელით გაანაწილა
/// </summary>
public sealed record LearnedPayer(int StudentHumanId, string? PersonalNumber, string? AccountIban);
