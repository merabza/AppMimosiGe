using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Balances.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class BalanceOperationsTests
{
    private static DateTime At(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    private static ChargeData Charge(int id, int scId, DateTime lessonDt, decimal fee = 48m, float fourWeekHours = 8f,
        float hoursCount = 1f)
    {
        return new ChargeData(id, scId, lessonDt, "English", fee, fourWeekHours, hoursCount);
    }

    [Theory]
    [InlineData(48, 8f, 1f, -6)]
    [InlineData(48, 8f, 1.5f, -9)]
    [InlineData(100, 12f, 2f, -16.6667)]
    [InlineData(60.5, 6f, 0.5f, -5.0417)]
    public void ChargeAmount_IsMinusTheHourFeeTimesTheHours(decimal fee, float fourWeekHours, float hoursCount,
        decimal expected)
    {
        Assert.Equal(expected,
            BalanceOperations.RoundMoney(BalanceOperations.ChargeAmount(fee, fourWeekHours, hoursCount)));
    }

    //the hour fee is not rounded before it is multiplied
    [Fact]
    public void ChargeAmount_KeepsTheFullPrecision()
    {
        Assert.Equal(-100m / 12m * 2m, BalanceOperations.ChargeAmount(100m, 12f, 2f));
    }

    [Fact]
    public void ChargeAmount_FourWeekHoursZero_IsZero()
    {
        Assert.Equal(0m, BalanceOperations.ChargeAmount(48m, 0f, 2f));
    }

    //Access turns the Single hours into Double exactly; 0.1f is 0.100000001490116... there, not 0.1
    [Fact]
    public void ChargeAmount_SingleHours_GoThroughDoubleLikeAccess()
    {
        Assert.Equal(-6m * (decimal)(double)0.1f, BalanceOperations.ChargeAmount(48m, 8f, 0.1f));
        Assert.NotEqual(-0.6m, BalanceOperations.ChargeAmount(48m, 8f, 0.1f));
    }

    [Fact]
    public void Build_MakesChargesNegativeWithTheCourseAndPaymentsPositiveWithTheirDocument()
    {
        // Act
        List<BalanceOperation> operations = BalanceOperations.Build([Charge(7, 3, At(9, 10, 15))],
            [new PaymentData(7, 3, At(9, 11), "bank 1", 100m)]);

        // Assert
        Assert.Equal([
            new BalanceOperation(false, 7, 3, At(9, 10, 15), "English", -6m),
            new BalanceOperation(true, 7, 3, At(9, 11), "bank 1", 100m)
        ], operations);
    }

    [Fact]
    public void Build_OrdersByDateThenPaymentsFirstThenId()
    {
        // Arrange: a charge at midnight ties with the payments of that day
        ChargeData[] charges = [Charge(9, 1, At(9, 12, 10)), Charge(3, 2, At(9, 12)), Charge(2, 1, At(9, 12, 10))];
        PaymentData[] payments = [new(8, 1, At(9, 12), null, 10m), new(4, 2, At(9, 12), null, 20m)];

        // Act
        List<BalanceOperation> operations = BalanceOperations.Build(charges, payments);

        // Assert
        Assert.Equal([(true, 4), (true, 8), (false, 3), (false, 2), (false, 9)],
            operations.ConvertAll(o => (o.IsPayment, o.Id)));
    }

    [Theory]
    [InlineData(1.23455, 1.2346)]
    [InlineData(1.23445, 1.2344)]
    [InlineData(-16.666666666666666666666666667, -16.6667)]
    [InlineData(5, 5)]
    public void RoundMoney_RoundsToFourDecimals(decimal amount, decimal expected)
    {
        Assert.Equal(expected, BalanceOperations.RoundMoney(amount));
    }
}
