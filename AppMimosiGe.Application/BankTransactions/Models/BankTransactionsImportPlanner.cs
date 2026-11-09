using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Balances.Models;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ჩარიცხვის ერთი წილი: მოსწავლე, მისი მიმდინარე წლის კონტრაქტი (გადახდა მასზე იწერება) და თანხა
/// </summary>
public sealed record PlannedShare(int StudentHumanId, int StudentContractId, decimal Amount);

/// <summary>
///     ერთი ახალი ჩარიცხვა: რომელმა წესმა იპოვა მოსწავლე(ები) და წილები (ცარიელი = გაუნაწილებელი)
/// </summary>
public sealed record PlannedTransaction(BankMovement Movement, EPaymentMatchStep Step, IReadOnlyList<PlannedShare> Shares);

/// <param name="Match">მოსწავლეების საძიებო მონაცემები (PaymentMatcher)</param>
/// <param name="CurrentContractByStudent">მოსწავლე → მიმდინარე წლის კონტრაქტი</param>
/// <param name="Operations">მიმდინარე მოსწავლეების ყველა კონტრაქტის ოპერაციები (BalanceOperations.Build)</param>
/// <param name="StudentByContract">კონტრაქტი → მოსწავლე (მოსწავლის ანგარიში, StudentAccounts)</param>
public sealed record ImportPlanInput(
    PaymentMatchInput Match,
    IReadOnlyDictionary<int, int> CurrentContractByStudent,
    IReadOnlyList<BalanceOperation> Operations,
    IReadOnlyDictionary<int, int> StudentByContract);

/// <summary>
///     ახალი ჩარიცხვების ავტომატური განაწილება (TBC_INTEGRATION.md §4, D163, D164): თითო ჩარიცხვას PaymentMatcher
///     მოსწავლეებს უძებნის; ერთი მოსწავლე მთელ თანხას იღებს, რამდენიმე კი PaymentDistributor-ით ინაწილებს, ჩარიცხვის
///     თარიღის ბალანსით (მოსწავლის ანგარიში: ყველა კონტრაქტი, ამ დღის ჩათვლით) და წლის ბოლომდე გადასახდელით (ყველა
///     ოპერაცია). ჩარიცხვები თავიანთი რიგით მუშავდება და წინა ჩარიცხვების წილები შემდეგის ბალანსში ითვლება. 0-იანი წილი
///     გადახდად არ იქცევა. მოსწავლე ვერ იპოვა ან თანხა დადებითი არ არის → წილები ცარიელია
/// </summary>
public static class BankTransactionsImportPlanner
{
    public static List<PlannedTransaction> Plan(IEnumerable<BankMovement> movements, ImportPlanInput input)
    {
        Dictionary<int, List<BalanceOperation>> operations = input.Operations
            .GroupBy(o => StudentAccounts.AccountOf(input.StudentByContract, o.StudentContractId))
            .ToDictionary(g => g.Key, g => g.ToList());

        List<PlannedTransaction> plan = [];
        foreach (BankMovement movement in movements)
        {
            PaymentMatch match = PaymentMatcher.Match(movement, input.Match);
            List<int> students = [.. match.StudentHumanIds.Where(input.CurrentContractByStudent.ContainsKey)];
            if (students.Count == 0 || movement.Amount <= 0m)
            {
                plan.Add(new PlannedTransaction(movement, students.Count == 0 ? EPaymentMatchStep.None : match.Step,
                    []));
                continue;
            }

            List<DistributionShare> shares = students.Count == 1
                ? [new DistributionShare(students[0], movement.Amount)]
                : PaymentDistributor.Distribute(movement.Amount,
                    students.Select(student => Account(student, movement.Date, input, operations)));

            List<PlannedShare> planned = [];
            foreach (DistributionShare share in shares.Where(s => s.Amount != 0m))
            {
                int studentContractId = input.CurrentContractByStudent[share.StudentHumanId];
                planned.Add(new PlannedShare(share.StudentHumanId, studentContractId, share.Amount));
                OperationsOf(AccountKey(share.StudentHumanId, input), operations).Add(new BalanceOperation(true, 0,
                    studentContractId, movement.Date.Date, null, share.Amount));
            }

            plan.Add(new PlannedTransaction(movement, match.Step, planned));
        }

        return plan;
    }

    private static DistributionAccount Account(int studentHumanId, DateTime date, ImportPlanInput input,
        Dictionary<int, List<BalanceOperation>> operations)
    {
        List<BalanceOperation> accountOperations = OperationsOf(AccountKey(studentHumanId, input), operations);
        DateTime dayEnd = date.Date.AddDays(1);
        decimal balance = accountOperations.Where(o => o.OperationDate < dayEnd).Sum(o => o.Amount);
        decimal mustPayToEnd = -accountOperations.Sum(o => o.Amount);
        return new DistributionAccount(studentHumanId, BalanceOperations.RoundMoney(balance),
            BalanceOperations.RoundMoney(mustPayToEnd));
    }

    //მოსწავლის ანგარიშის გასაღები: მისი მიმდინარე კონტრაქტის ანგარიში (StudentAccounts)
    private static int AccountKey(int studentHumanId, ImportPlanInput input)
    {
        return StudentAccounts.AccountOf(input.StudentByContract, input.CurrentContractByStudent[studentHumanId]);
    }

    private static List<BalanceOperation> OperationsOf(int account, Dictionary<int, List<BalanceOperation>> operations)
    {
        if (!operations.TryGetValue(account, out List<BalanceOperation>? list))
        {
            list = [];
            operations[account] = list;
        }

        return list;
    }
}
