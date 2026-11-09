using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class PaymentDistributorTests
{
    private static List<decimal> Shares(decimal amount, params DistributionAccount[] accounts)
    {
        List<DistributionShare> shares = PaymentDistributor.Distribute(amount, accounts);
        Assert.Equal(amount, shares.Sum(s => s.Amount));
        Assert.All(shares, s => Assert.True(s.Amount >= 0m));
        return [.. shares.Select(s => s.Amount)];
    }

    [Fact]
    public void Distribute_NoStudents_IsEmpty()
    {
        Assert.Empty(PaymentDistributor.Distribute(100m, []));
    }

    [Fact]
    public void Distribute_OneStudent_GetsEverything()
    {
        Assert.Equal([150.5m], Shares(150.5m, new DistributionAccount(1, 20m, 0m)));
    }

    [Fact]
    public void Distribute_ResultIsOrderedByStudent()
    {
        List<DistributionShare> shares = PaymentDistributor.Distribute(100m,
            [new DistributionAccount(7, -30m, 30m), new DistributionAccount(2, -70m, 70m)]);

        Assert.Equal([new DistributionShare(2, 70m), new DistributionShare(7, 30m)], shares);
    }

    [Fact]
    public void Distribute_LessThanTheDebts_IsProportionalToTheDebts()
    {
        // debts 100 and 300, 200 → 50 and 150; the student with a deposit gets nothing
        Assert.Equal([50m, 150m, 0m],
            Shares(200m, new DistributionAccount(1, -100m, 500m), new DistributionAccount(2, -300m, 300m),
                new DistributionAccount(3, 40m, 200m)));
    }

    [Fact]
    public void Distribute_LessThanTheDebts_RoundsToWholeLariAndGivesTheTetriToTheLargestShare()
    {
        // exact 33.33 / 66.67 of 100.50: whole lari 33 + 67 (largest remainder), 0.50 to the largest
        Assert.Equal([33m, 67.5m], Shares(100.5m, new DistributionAccount(1, -50m, 0m), new DistributionAccount(2, -100m, 0m)));
    }

    [Fact]
    public void Distribute_LessThanTheDebts_EqualRemaindersGoToTheFirstStudent()
    {
        // 100 by three equal debts: 33 each, the leftover lari to the first
        Assert.Equal([34m, 33m, 33m],
            Shares(100m, new DistributionAccount(1, -60m, 0m), new DistributionAccount(2, -60m, 0m),
                new DistributionAccount(3, -60m, 0m)));
    }

    [Fact]
    public void Distribute_SmallAmountAmongMany_NeverGoesNegative()
    {
        Assert.Equal([1m, 1m, 0m, 0m],
            Shares(2m, new DistributionAccount(1, -10m, 0m), new DistributionAccount(2, -10m, 0m),
                new DistributionAccount(3, -10m, 0m), new DistributionAccount(4, -10m, 0m)));
    }

    [Fact]
    public void Distribute_ExactlyTheDebts_CoversEveryDebtToTheTetri()
    {
        Assert.Equal([47.35m, 52.65m, 0m],
            Shares(100m, new DistributionAccount(1, -47.35m, 300m), new DistributionAccount(2, -52.65m, 100m),
                new DistributionAccount(3, 10m, 500m)));
    }

    [Fact]
    public void Distribute_DebtsAreRoundedToTetri()
    {
        // balances carry 4 decimals; payments have 2
        Assert.Equal([33.33m, 66.67m],
            Shares(100m, new DistributionAccount(1, -33.3333m, 0m), new DistributionAccount(2, -66.6667m, 0m)));
    }

    [Fact]
    public void Distribute_MoreThanTheDebts_CoversTheDebtsAndSplitsTheRestByWhatRemainsToTheEnd()
    {
        // debts 100 + 0 + 0; to the end beyond the debt: 100, 300, 0 (deposit larger than the future); excess 400 → 100, 300
        Assert.Equal([200m, 300m, 0m],
            Shares(500m, new DistributionAccount(1, -100m, 200m), new DistributionAccount(2, 50m, 300m),
                new DistributionAccount(3, 500m, -100m)));
    }

    [Fact]
    public void Distribute_MoreThanTheDebts_ABigDebtIsCoveredEvenWithLittleLeftToTheEnd()
    {
        // whole amount by MustPayToEnd would give student 1 only 18; the debt comes first (Q-T1)
        Assert.Equal([100m, 100m], Shares(200m, new DistributionAccount(1, -100m, 100m), new DistributionAccount(2, 0m, 1000m)));
    }

    [Fact]
    public void Distribute_MoreThanTheDebts_TheExcessTetriGoToTheLargestShare()
    {
        // debts 20 + 10, excess 111.30 by equal weights: 55.5 each → 56 + 55 (leftover lari to the first), 0.30 to the
        // largest share
        Assert.Equal([76.3m, 65m],
            Shares(141.3m, new DistributionAccount(1, -20m, 120m), new DistributionAccount(2, -10m, 110m)));
    }

    [Fact]
    public void Distribute_NothingLeftToTheEnd_SplitsTheExcessEqually()
    {
        Assert.Equal([60m, 50m, 50m],
            Shares(160m, new DistributionAccount(1, -10m, 10m), new DistributionAccount(2, 0m, 0m),
                new DistributionAccount(3, 30m, -30m)));
    }

    [Fact]
    public void Distribute_NoDebts_IsProportionalToWhatRemainsToTheEnd()
    {
        Assert.Equal([25m, 75m], Shares(100m, new DistributionAccount(1, 0m, 100m), new DistributionAccount(2, 5m, 300m)));
    }
}
