using System.Collections.Generic;
using AppMimosiGe.Application.Balances.Models;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <param name="Match">მოსწავლეების საძიებო მონაცემები (PaymentMatcher)</param>
/// <param name="CurrentContractByStudent">მოსწავლე → მიმდინარე წლის კონტრაქტი</param>
/// <param name="Operations">მიმდინარე მოსწავლეების ყველა კონტრაქტის ოპერაციები (BalanceOperations.Build)</param>
/// <param name="StudentByContract">კონტრაქტი → მოსწავლე (მოსწავლის ანგარიში, StudentAccounts)</param>
public sealed record ImportPlanInput(
    PaymentMatchInput Match,
    IReadOnlyDictionary<int, int> CurrentContractByStudent,
    IReadOnlyList<BalanceOperation> Operations,
    IReadOnlyDictionary<int, int> StudentByContract);
