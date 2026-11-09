using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class BankTransactionsImportPlannerTests
{
    private static readonly DateTime Day = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Unspecified);

    //students 1 and 2 are siblings (payer 100), 3 has another payer; 1's current contract is 11, 2's is 12, 3's is 13;
    //1 also has last year's contract 21
    private static readonly PaymentMatchInput Match = new([
            new MatchStudent(1, "ნინო", "ბერიძე"),
            new MatchStudent(2, "გიორგი", "ბერიძე"),
            new MatchStudent(3, "ლუკა", "გელაშვილი")
        ], new Dictionary<string, int>(),
        [
            new MatchPayer(100, "01001000100", "თამარ", "ბერიძე", [1, 2]),
            new MatchPayer(101, "01001000101", "ეკა", "კაპანაძე", [3])
        ], []);

    private static readonly Dictionary<int, int> CurrentContracts = new() { [1] = 11, [2] = 12, [3] = 13 };
    private static readonly Dictionary<int, int> StudentByContract = new() { [11] = 1, [21] = 1, [12] = 2, [13] = 3 };

    private static BankMovement Movement(string id, decimal amount, string? purpose, string? taxCode = null,
        int day = 0)
    {
        return new BankMovement(id, Day.AddDays(day), null, amount, "GEL", purpose, null, null, null, null, null,
            "TBCBGE22", taxCode, null);
    }

    private static BalanceOperation Charge(int scId, decimal amount, int day = 0)
    {
        return new BalanceOperation(false, 0, scId, Day.AddDays(day).AddHours(15), "course", -amount);
    }

    private static List<PlannedTransaction> Plan(IReadOnlyList<BalanceOperation> operations,
        params BankMovement[] movements)
    {
        return BankTransactionsImportPlanner.Plan(movements,
            new ImportPlanInput(Match, CurrentContracts, operations, StudentByContract));
    }

    [Fact]
    public void Plan_OneStudent_GetsTheWholeAmountOnHerCurrentContract()
    {
        PlannedTransaction planned = Assert.Single(Plan([], Movement("1", 150m, "ლუკა გელაშვილი")));

        Assert.Equal(EPaymentMatchStep.StudentName, planned.Step);
        Assert.Equal([new PlannedShare(3, 13, 150m)], planned.Shares);
    }

    [Fact]
    public void Plan_NobodyFound_IsUnallocated()
    {
        PlannedTransaction planned = Assert.Single(Plan([], Movement("1", 150m, "სხვა")));

        Assert.Equal(EPaymentMatchStep.None, planned.Step);
        Assert.Empty(planned.Shares);
    }

    [Fact]
    public void Plan_NonPositiveAmount_IsUnallocatedButKeepsTheStep()
    {
        PlannedTransaction planned = Assert.Single(Plan([], Movement("1", 0m, "ლუკა გელაშვილი")));

        Assert.Equal(EPaymentMatchStep.StudentName, planned.Step);
        Assert.Empty(planned.Shares);
    }

    // the siblings' balance is the student's account up to the transaction day (last year's contract too, a charge of
    // the next day not): 1 owes 30 + 20, 2 owes 50; the debts are equal, so 60 splits equally
    [Fact]
    public void Plan_SeveralStudents_SplitByTheirDebtsOnTheTransactionDay()
    {
        PlannedTransaction planned = Assert.Single(Plan(
            [Charge(11, 30m), Charge(21, 20m, -200), Charge(12, 50m), Charge(12, 100m, 1)],
            Movement("1", 60m, "სწავლა", "01001000100")));

        Assert.Equal(EPaymentMatchStep.PayerPersonalNumber, planned.Step);
        Assert.Equal([new PlannedShare(1, 11, 30m), new PlannedShare(2, 12, 30m)], planned.Shares);
    }

    // after the debts the excess goes by what is left to pay to the year end (all operations): 1 has no more, 2 has 100
    [Fact]
    public void Plan_Excess_GoesByTheRestToTheYearEnd()
    {
        PlannedTransaction planned = Assert.Single(Plan([Charge(11, 30m), Charge(12, 50m), Charge(12, 100m, 1)],
            Movement("1", 130m, "სწავლა", "01001000100")));

        Assert.Equal([new PlannedShare(1, 11, 30m), new PlannedShare(2, 12, 100m)], planned.Shares);
    }

    // a zero share makes no payment
    [Fact]
    public void Plan_ZeroShares_AreDropped()
    {
        PlannedTransaction planned = Assert.Single(Plan([Charge(12, 50m)],
            Movement("1", 40m, "სწავლა", "01001000100")));

        Assert.Equal([new PlannedShare(2, 12, 40m)], planned.Shares);
    }

    // an earlier transaction's shares count in the next one's balances: the first pays 2's debt, so the second goes
    // to 1 alone
    [Fact]
    public void Plan_EarlierSharesCountInTheNextBalances()
    {
        List<PlannedTransaction> plan = Plan([Charge(11, 50m), Charge(12, 50m)],
            Movement("1", 50m, "გიორგი ბერიძე"), Movement("2", 50m, "სწავლა", "01001000100", 1));

        Assert.Equal([new PlannedShare(2, 12, 50m)], plan[0].Shares);
        Assert.Equal([new PlannedShare(1, 11, 50m)], plan[1].Shares);
        Assert.Equal(["1", "2"], plan.Select(p => p.Movement.MovementId));
    }

    // a student without a balance account entry (no contracts in the map) is her contract's own account
    [Fact]
    public void Plan_StudentOutsideTheAccountMap_UsesHerContract()
    {
        List<PlannedTransaction> plan = BankTransactionsImportPlanner.Plan(
            [Movement("1", 60m, "სწავლა", "01001000100")],
            new ImportPlanInput(Match, CurrentContracts, [Charge(11, 40m), Charge(12, 20m)],
                new Dictionary<int, int>()));

        Assert.Equal([new PlannedShare(1, 11, 40m), new PlannedShare(2, 12, 20m)], Assert.Single(plan).Shares);
    }
}
