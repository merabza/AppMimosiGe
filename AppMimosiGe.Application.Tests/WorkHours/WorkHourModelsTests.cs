using System;
using System.Collections.Generic;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.WorkHours;

public sealed class WorkHourModelsTests
{
    private static DateTime At(int day, int hour, int minute = 0, int second = 0) =>
        new(2026, 9, day, hour, minute, second, DateTimeKind.Unspecified);

    private static WorkHourEmployee Employee(DateTime contractDate, DateTime? contractEndDate) =>
        new(1, "Alpha Ann / T3.01", contractDate, contractEndDate, null, null);

    [Fact]
    public void Hours_IsTheDurationRoundedToTwoDecimals()
    {
        Assert.Equal(8.08m, WorkHourDurations.Hours(At(15, 9, 55), At(15, 18)));
        Assert.Equal(1.5m, WorkHourDurations.Hours(At(15, 18), At(15, 19, 30)));
        //20 seconds are 0.0056 hours
        Assert.Equal(0.01m, WorkHourDurations.Hours(At(15, 18), At(15, 18, 0, 20)));
    }

    // a record without an end has no duration yet
    [Fact]
    public void Hours_WithoutEnd_IsNull()
    {
        Assert.Null(WorkHourDurations.Hours(At(15, 9, 55), null));
    }

    // per employee: the exact durations of the finished records summed (rounded at the end) and every record counted;
    // sorted by name
    [Fact]
    public void Totals_SumTheFinishedRecordsAndCountAllOfThemPerEmployee()
    {
        List<WorkHoursTotalResponse> totals = WorkHourDurations.Totals([
            new WorkHourTimeData(5, "Beta Bob / T3.05", At(15, 18), At(15, 20)),
            new WorkHourTimeData(1, "Alpha Ann / T3.01", At(15, 12), At(15, 12, 0, 20)),
            new WorkHourTimeData(1, "Alpha Ann / T3.01", At(16, 12), At(16, 12, 0, 20)),
            new WorkHourTimeData(1, "Alpha Ann / T3.01", At(17, 12), null),
            new WorkHourTimeData(15, "Alpha Ann / T3.10", At(17, 9), null)
        ]);

        Assert.Equal([
            //two times 20 seconds: 0.0111 hours, while the rounded records would sum up to 0.02
            new WorkHoursTotalResponse(1, "Alpha Ann / T3.01", 0.01m, 3),
            new WorkHoursTotalResponse(15, "Alpha Ann / T3.10", 0m, 1),
            new WorkHoursTotalResponse(5, "Beta Bob / T3.05", 2m, 1)
        ], totals);
    }

    // two contracts may give one name only in theory (the contract number is unique); the id breaks the tie
    [Fact]
    public void Totals_SameName_AreSortedById()
    {
        List<WorkHoursTotalResponse> totals = WorkHourDurations.Totals([
            new WorkHourTimeData(7, "Same", At(15, 12), At(15, 13)),
            new WorkHourTimeData(3, "Same", At(15, 12), At(15, 14))
        ]);

        Assert.Equal([3, 7], totals.ConvertAll(t => t.TeacherContractId));
    }

    [Fact]
    public void Totals_WithoutRecords_IsEmpty()
    {
        Assert.Empty(WorkHourDurations.Totals([]));
    }

    // in force from the contract's date up to its end date, both days included; times of day do not matter
    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void IsActiveOn_IsTheContractPeriod(int day, bool expected)
    {
        WorkHourEmployee employee = Employee(At(15, 10), At(20, 8));

        Assert.Equal(expected, employee.IsActiveOn(At(day, 23, 59)));
    }

    // without an end date the contract never ends
    [Fact]
    public void IsActiveOn_WithoutEndDate_IsActiveFromItsDate()
    {
        WorkHourEmployee employee = Employee(At(15, 0), null);

        Assert.False(employee.IsActiveOn(At(14, 12)));
        Assert.True(employee.IsActiveOn(new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    // Access: Nz(txtLuft, 0), a negative luft is 0
    [Theory]
    [InlineData(null, 0)]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    public void LuftMinutes_IsZeroForNoneOrNegative(int? luft, int expected)
    {
        Assert.Equal(expected, WorkTimeFixRules.LuftMinutes(luft));
    }

    // the server's local (Georgian) time, cut to the second like Access's Now()
    [Fact]
    public void Now_IsTheLocalTimeToTheSecond()
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(2026, 9, 30, 21, 30, 15, TimeSpan.Zero).AddTicks(9_999_999));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.CreateCustomTimeZone("Georgia",
            TimeSpan.FromHours(4), "Georgia", "Georgia"));

        DateTime now = WorkTimeFixRules.Now(timeProvider.Object);

        Assert.Equal(new DateTime(2026, 10, 1, 1, 30, 15, DateTimeKind.Unspecified), now);
        Assert.Equal(DateTimeKind.Unspecified, now.Kind);
    }

    [Fact]
    public void MaxLuftMinutes_IsThirty()
    {
        Assert.Equal(30, WorkTimeFixRules.MaxLuftMinutes);
    }
}
