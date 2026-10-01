using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Balances.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class NextPayDateCalculatorTests
{
    private static readonly DateTime Today = At(10, 1);

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static BalanceOperation Charge(int id, DateTime date, decimal amount = -10m) =>
        new(false, id, 3, date, "English", amount);

    private static BalanceOperation Payment(int id, DateTime date, decimal amount) =>
        new(true, id, 3, date, null, amount);

    private static DateTime? NextPayDate(List<BalanceOperation> operations) =>
        NextPayDateCalculator.Calculate(operations, NextPayDateCalculator.LastPayDate(operations, Today));

    //no payment: the last pay date is today, so the first lesson is the next pay date
    [Fact]
    public void Calculate_NoPayments_IsTheFirstCharge()
    {
        Assert.Equal(At(9, 3, 15), NextPayDate([Charge(1, At(9, 3, 15)), Charge(2, At(9, 10, 15))]));
    }

    [Fact]
    public void Calculate_NoPaymentsAndOnlyFutureLessons_IsTheFirstFutureLesson()
    {
        Assert.Equal(At(10, 5, 15), NextPayDate([Charge(1, At(10, 5, 15)), Charge(2, At(10, 12, 15))]));
    }

    [Fact]
    public void Calculate_NoOperations_IsNull()
    {
        Assert.Null(NextPayDate([]));
    }

    //the payment covers every charge, the future ones too
    [Fact]
    public void Calculate_FullyCovered_IsNull()
    {
        Assert.Null(NextPayDate([
            Payment(1, At(9, 1), 30m), Charge(2, At(9, 3, 15)), Charge(3, At(9, 10, 15)), Charge(4, At(10, 8, 15))
        ]));
    }

    [Fact]
    public void Calculate_CoveredExactly_IsNull()
    {
        Assert.Null(NextPayDate([Payment(1, At(9, 1), 20m), Charge(2, At(9, 3, 15)), Charge(3, At(9, 10, 15))]));
    }

    //the payment covers two lessons; the third one, after the last payment, makes the debt
    [Fact]
    public void Calculate_DebtAfterTheLastPayment_IsTheFirstUncoveredLesson()
    {
        Assert.Equal(At(9, 17, 15), NextPayDate([
            Payment(1, At(9, 1), 20m), Charge(2, At(9, 3, 15)), Charge(3, At(9, 10, 15)), Charge(4, At(9, 17, 15)),
            Charge(5, At(9, 24, 15))
        ]));
    }

    //a debt before a payment, which the payment covers: the date is dropped and the next debt counts
    [Fact]
    public void Calculate_TemporarilyNegativeThenCovered_IsTheNextDebt()
    {
        Assert.Equal(At(9, 24, 15), NextPayDate([
            Charge(1, At(9, 3, 15)), Charge(2, At(9, 10, 15)), Payment(3, At(9, 12), 30m), Charge(4, At(9, 17, 15)),
            Charge(5, At(9, 24, 15)), Charge(6, At(10, 1, 15))
        ]));
    }

    //a debt that the last payment does not cover keeps its first date
    [Fact]
    public void Calculate_DebtThePaymentDoesNotCover_KeepsItsFirstDate()
    {
        Assert.Equal(At(9, 3, 15), NextPayDate([
            Charge(1, At(9, 3, 15)), Charge(2, At(9, 10, 15)), Payment(3, At(9, 12), 15m), Charge(4, At(9, 17, 15))
        ]));
    }

    //the debt is counted only until an operation after the last payment; a later payment would not cancel it
    [Fact]
    public void Calculate_StopsAtTheFirstDebtAfterTheLastPayDate()
    {
        // Arrange: the last pay date is given (01.09), so the payment of 20.09 comes after the stop
        List<BalanceOperation> operations =
        [
            Payment(1, At(9, 1), 10m), Charge(2, At(9, 3, 15)), Charge(3, At(9, 10, 15)), Payment(4, At(9, 20), 50m)
        ];

        // Act
        DateTime? nextPayDate = NextPayDateCalculator.Calculate(operations, At(9, 1));

        // Assert
        Assert.Equal(At(9, 10, 15), nextPayDate);
    }

    //the lesson of the last pay day is later than the pay date (midnight), so it stops the search
    [Fact]
    public void Calculate_LessonOnTheLastPayDay_IsAfterThePayDate()
    {
        Assert.Equal(At(9, 12, 15), NextPayDate([
            Payment(1, At(9, 1), 10m), Charge(2, At(9, 3, 15)), Payment(3, At(9, 12), 5m), Charge(4, At(9, 12, 15)),
            Charge(5, At(9, 13, 15))
        ]));
    }

    //VBA keeps the sum in Currency (4 decimals): -0.00004 is no debt there
    [Fact]
    public void Calculate_RoundsTheSumLikeCurrency()
    {
        Assert.Null(NextPayDateCalculator.Calculate([Payment(1, At(9, 1), 10m), Charge(2, At(9, 3, 15), -10.00004m)],
            At(9, 1)));
    }

    //rounded after every operation, not once at the end: three times -0.00004 stays 0.0000
    [Fact]
    public void Calculate_RoundsAfterEveryOperation()
    {
        Assert.Null(NextPayDateCalculator.Calculate([
            Charge(1, At(9, 3, 15), -0.00004m), Charge(2, At(9, 10, 15), -0.00004m), Charge(3, At(9, 17, 15), -0.00004m)
        ], At(9, 1)));
    }

    [Fact]
    public void Calculate_CurrencyRoundingIsBankers()
    {
        // Arrange: -0.00005 rounds to 0.0000 (even), so there is no debt
        List<BalanceOperation> operations = [Payment(1, At(9, 1), 10m), Charge(2, At(9, 3, 15), -10.00005m)];

        // Act + Assert
        Assert.Null(NextPayDateCalculator.Calculate(operations, At(9, 1)));
        Assert.Equal(At(9, 3, 15),
            NextPayDateCalculator.Calculate([Payment(1, At(9, 1), 10m), Charge(2, At(9, 3, 15), -10.00015m)],
                At(9, 1)));
    }

    [Fact]
    public void LastPayDate_IsTheLatestPayment()
    {
        Assert.Equal(At(9, 20), NextPayDateCalculator.LastPayDate([
            Payment(1, At(9, 20), 10m), Charge(2, At(9, 25, 15)), Payment(3, At(9, 5), 10m)
        ], Today));
    }

    [Fact]
    public void LastPayDate_NoPayments_IsToday()
    {
        Assert.Equal(Today, NextPayDateCalculator.LastPayDate([Charge(1, At(11, 3, 15))], Today.AddHours(17)));
    }
}
