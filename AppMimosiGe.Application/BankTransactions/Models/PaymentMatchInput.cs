using System.Collections.Generic;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <param name="Students">მიმდინარე წლის მოსწავლეები</param>
/// <param name="StudentByContractNumber">ყველა წლის კონტრაქტის ნომერი → მოსწავლე</param>
/// <param name="Payers">მიმდინარე წლის კონტრაქტების გადამხდელები</param>
/// <param name="LearnedPayers">დასწავლილი გადამხდელები</param>
public sealed record PaymentMatchInput(
    IReadOnlyList<MatchStudent> Students,
    IReadOnlyDictionary<string, int> StudentByContractNumber,
    IReadOnlyList<MatchPayer> Payers,
    IReadOnlyList<LearnedPayer> LearnedPayers);
