using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Balances.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class StatementCalculatorTests
{
    //one contract: payment 100 (01.09), charges of 6 (05.09 15:00, 12.09 15:00), payment 20 (12.09, before the
    //charge of that day), charge of 6 (30.09 15:00), charge of 6.33333 (01.10 10:00)
    private static readonly List<BalanceOperation> Operations =
    [
        Payment(1, At(9, 1), 100m),
        Charge(10, At(9, 5, 15), -6m),
        Payment(2, At(9, 12), 20m),
        Charge(11, At(9, 12, 15), -6m),
        Charge(12, At(9, 30, 15), -6m),
        Charge(13, At(10, 1, 10), -6.33333m)
    ];

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static BalanceOperation Charge(int id, DateTime date, decimal amount) =>
        new(false, id, 3, date, "English", amount);

    private static BalanceOperation Payment(int id, DateTime date, decimal amount) =>
        new(true, id, 3, date, null, amount);

    [Fact]
    public void Build_WithoutDates_ShowsAllOperationsWithTheirRunningTotals()
    {
        // Act
        Statement statement = StatementCalculator.Build(Operations, null, null);

        // Assert
        Assert.Equal([100m, 94m, 114m, 108m, 102m, 95.6667m], statement.Rows.Select(r => r.RunningTotal));
        Assert.Equal(Operations, statement.Rows.Select(r => r.Operation));
        Assert.Equal(0m, statement.StartBalance);
        //without "date to" the end balance takes every operation (the last running total)
        Assert.Equal(95.6667m, statement.EndBalance);
    }

    [Fact]
    public void Build_DateFrom_KeepsTheRunningTotalFromTheBeginningAndGivesTheStartBalance()
    {
        // Act
        Statement statement = StatementCalculator.Build(Operations, At(9, 12, 9), null);

        // Assert: from the start of 12.09 (the time is dropped), the payment of that day is included
        Assert.Equal([2, 11, 12, 13], statement.Rows.Select(r => r.Operation.Id));
        Assert.Equal([114m, 108m, 102m, 95.6667m], statement.Rows.Select(r => r.RunningTotal));
        Assert.Equal(94m, statement.StartBalance);
        Assert.Equal(95.6667m, statement.EndBalance);
    }

    [Fact]
    public void Build_DateTo_IncludesTheWholeDayAndGivesTheEndBalance()
    {
        // Act
        Statement statement = StatementCalculator.Build(Operations, null, At(9, 30));

        // Assert: the charge of 30.09 15:00 is included, the one of 01.10 is not
        Assert.Equal([1, 10, 2, 11, 12], statement.Rows.Select(r => r.Operation.Id));
        Assert.Equal(0m, statement.StartBalance);
        Assert.Equal(102m, statement.EndBalance);
    }

    [Fact]
    public void Build_BothDates_StartPlusTheRowsIsTheEndBalance()
    {
        // Act
        Statement statement = StatementCalculator.Build(Operations, At(9, 5), At(9, 12));

        // Assert
        Assert.Equal([10, 2, 11], statement.Rows.Select(r => r.Operation.Id));
        Assert.Equal(100m, statement.StartBalance);
        Assert.Equal(108m, statement.EndBalance);
        Assert.Equal(statement.EndBalance, statement.StartBalance + statement.Rows.Sum(r => r.Operation.Amount));
        Assert.Equal(statement.EndBalance, statement.Rows[^1].RunningTotal);
    }

    [Fact]
    public void Build_SameDay_FromAndToTheSameDayShowsThatDay()
    {
        // Act
        Statement statement = StatementCalculator.Build(Operations, At(9, 12), At(9, 12));

        // Assert
        Assert.Equal([2, 11], statement.Rows.Select(r => r.Operation.Id));
        Assert.Equal(94m, statement.StartBalance);
        Assert.Equal(108m, statement.EndBalance);
    }

    [Fact]
    public void Build_NoOperationsInTheRange_HasNoRowsButBothBalances()
    {
        // Act
        Statement statement = StatementCalculator.Build(Operations, At(9, 13), At(9, 29));

        // Assert
        Assert.Empty(statement.Rows);
        Assert.Equal(108m, statement.StartBalance);
        Assert.Equal(108m, statement.EndBalance);
    }

    //operations at the same time (one lesson of several students, or two groups at once) accumulate in the
    //statement order; Access gave all of them the total of the time
    [Fact]
    public void Build_OperationsAtTheSameTime_AccumulateOneByOne()
    {
        // Arrange
        List<BalanceOperation> operations =
        [
            new(false, 5, 1, At(9, 7, 10), "English", -6m),
            new(false, 6, 2, At(9, 7, 10), "English", -9m),
            new(false, 7, 1, At(9, 7, 10), "Math", -3m)
        ];

        // Act
        Statement statement = StatementCalculator.Build(operations, null, null);

        // Assert
        Assert.Equal([-6m, -15m, -18m], statement.Rows.Select(r => r.RunningTotal));
    }

    [Fact]
    public void Build_RoundsTheTotalsButAccumulatesExactAmounts()
    {
        // Arrange: three charges of 100/3; rounding each running total must not drift
        decimal third = BalanceOperations.ChargeAmount(100m, 3f, 1f);
        List<BalanceOperation> operations =
        [
            new(false, 1, 1, At(9, 1, 10), "English", third),
            new(false, 2, 1, At(9, 2, 10), "English", third),
            new(false, 3, 1, At(9, 3, 10), "English", third),
            new(true, 4, 1, At(9, 4), null, 100m)
        ];

        // Act
        Statement statement = StatementCalculator.Build(operations, At(9, 3), null);

        // Assert
        Assert.Equal([-100m, 0m], statement.Rows.Select(r => r.RunningTotal));
        Assert.Equal(-66.6667m, statement.StartBalance);
        Assert.Equal(0m, statement.EndBalance);
    }

    [Fact]
    public void Build_NoOperations_IsEmpty()
    {
        // Act
        Statement statement = StatementCalculator.Build([], At(9, 1), At(9, 30));

        // Assert
        Assert.Empty(statement.Rows);
        Assert.Equal(0m, statement.StartBalance);
        Assert.Equal(0m, statement.EndBalance);
    }
}
