using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class DepositsCalculatorTests
{
    private static readonly DateTime Today = At(10, 1);
    private static readonly DateTime DateTo = At(10, 6);

    private readonly List<DepositContractData> _contracts = [];
    private readonly List<CrmMustPayDateData> _crmCalls = [];
    private readonly List<DepositGroupStudentData> _groupStudents = [];
    private readonly Dictionary<int, DateTime> _nextLessons = [];
    private readonly List<BalanceOperation> _operations = [];
    private readonly Dictionary<int, int> _studentByContract = [];
    private DateTime? _lastOperationMonth = At(11, 1).AddYears(1);

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //synthetic people only: never real names
    //every contract is its own student unless studentHumanId says otherwise (the student's account, part 20)
    private void Contract(int scId, string studentName = "Alpha Ann", int? desiredDay = null,
        DateTime? nextPayDate = null, int? studentHumanId = null, int academicYearId = 11)
    {
        int humanId = studentHumanId ?? 100 + scId;
        _studentByContract[scId] = humanId;
        _contracts.Add(new DepositContractData(scId, academicYearId, studentName, $"6.00{scId}", "555000001",
            "Payer Pat", "555000002", desiredDay, nextPayDate, humanId, new DateTime(2015 + academicYearId, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    private void Charge(int scId, DateTime date, decimal amount = -10m)
    {
        _operations.Add(new BalanceOperation(false, _operations.Count + 1, scId, date, "English", amount));
    }

    private void Payment(int scId, DateTime date, decimal amount)
    {
        _operations.Add(new BalanceOperation(true, _operations.Count + 1, scId, date, null, amount));
    }

    private DepositsResponse Build(EDepositsFilter filter = EDepositsFilter.None, decimal maximum = 0m,
        DateTime? dateTo = null)
    {
        DepositsInput input = new(_contracts, [.. _operations.OrderBy(o => o.OperationDate)], _nextLessons, _crmCalls,
            _groupStudents, _lastOperationMonth, _studentByContract);
        return DepositsCalculator.Build(input, new DepositsParameters(maximum, dateTo ?? DateTo, filter, Today));
    }

    private DepositRowResponse Row(int scId) => Assert.Single(Build().Rows, r => r.StudentContractId == scId);

    private static void AssertIds(DepositsResponse response, params int[] expected)
    {
        Assert.Equal(expected, response.Rows.Select(r => r.StudentContractId));
    }

    [Fact]
    public void Build_ShowsContractsBelowTheMaximumOrWithADesiredDayAmount()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15)); //-10: below 0
        Contract(2);
        Payment(2, At(9, 1), 50m); //+50: above
        Contract(3, desiredDay: 15);
        Payment(3, At(9, 1), 50m); //above, but has a desired day
        Contract(4); //no operation: no balance
        Contract(5);
        Payment(5, At(9, 1), 10m);
        Charge(5, At(9, 10, 15)); //0: not below 0

        // Act + Assert
        AssertIds(Build(), 1, 3);
    }

    [Fact]
    public void Build_MaximumMovesTheLimit()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15), -30m);
        Contract(2);
        Charge(2, At(9, 10, 15), -20m);

        // Act + Assert
        AssertIds(Build(maximum: -20m), 1);
        AssertIds(Build(maximum: -19.99m), 1, 2);
    }

    //"date to" counts until the end of the day; the lessons generated for later are not in the balance
    [Fact]
    public void Build_Balance_CountsOperationsUntilTheEndOfDateTo()
    {
        // Arrange
        Contract(1);
        Payment(1, At(9, 1), 100m);
        Charge(1, At(10, 6, 18), -30m);
        Charge(1, At(10, 7), -200m);

        // Act + Assert
        Assert.Equal(70m, Assert.Single(Build(maximum: 100m).Rows).Balance);
        Assert.Equal(-130m, Assert.Single(Build(dateTo: At(10, 7)).Rows).Balance);
    }

    [Fact]
    public void Build_Balance_IsRoundedToFourDecimals()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15), BalanceOperations.ChargeAmount(100m, 12f, 1f));

        // Act + Assert
        Assert.Equal(-8.3333m, Row(1).Balance);
    }

    //a desired day keeps a contract without operations until "date to" in the list, without a balance
    [Fact]
    public void Build_NoOperationsUntilDateTo_HasNoBalance()
    {
        // Arrange
        Contract(1, desiredDay: 20);
        Charge(1, At(10, 20, 15));

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Null(row.Balance);
        Assert.Equal(10m, row.DesiredDayAmount);
    }

    //vStudentMustPayOnDesiredDate: what is due until midnight after the "after next" pay date
    [Fact]
    public void Build_DesiredDay_GivesTheDatesAndTheAmountDueUntilTheDayAfterTheAfterNextDate()
    {
        // Arrange: today 01.10, day 15: next 15.10, after next 15.11, so operations until 16.11 00:00 count
        Contract(1, desiredDay: 15);
        Payment(1, At(9, 1), 30m);
        Charge(1, At(10, 20, 15), -40m);
        Payment(1, At(11, 16), 5m); //midnight of 16.11: counted
        Charge(1, At(11, 16, 15), -100m); //a lesson of 16.11: not counted

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Equal(15, row.DesiredMonthlyPaymentDay);
        Assert.Equal(At(10, 15), row.DesiredNextPayDate);
        Assert.Equal(At(11, 16), row.DesiredAfterNextPayDate);
        Assert.Equal(5m, row.DesiredDayAmount);
    }

    [Fact]
    public void Build_DesiredDayWithoutOperationsUntilThen_IsNotInTheList()
    {
        // Arrange: the only operation is after 16.11
        Contract(1, desiredDay: 15);
        Charge(1, At(12, 1, 15));

        // Act + Assert
        AssertIds(Build());
    }

    [Fact]
    public void Build_NoDesiredDay_HasNoDesiredDates()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Null(row.DesiredMonthlyPaymentDay);
        Assert.Null(row.DesiredNextPayDate);
        Assert.Null(row.DesiredAfterNextPayDate);
        Assert.Null(row.DesiredDayAmount);
    }

    //vStudentsFourWeekFee: the rows that do not end until "date to" (end of the day)
    [Fact]
    public void Build_FourWeekFee_SumsTheRowsActiveAfterDateTo()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, null, null));
        _groupStudents.Add(new DepositGroupStudentData(1, 60m, At(10, 7), null)); //ends after 06.10: active
        _groupStudents.Add(new DepositGroupStudentData(1, 70m, At(10, 6), null)); //ends on 06.10: not active
        _groupStudents.Add(new DepositGroupStudentData(2, 80m, null, null)); //another contract

        // Act + Assert
        Assert.Equal(108m, Row(1).FourWeekFee);
    }

    [Fact]
    public void Build_FourWeekFee_NoActiveRow_IsNull()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));
        _groupStudents.Add(new DepositGroupStudentData(1, 70m, At(9, 30), null));

        // Act + Assert
        Assert.Null(Row(1).FourWeekFee);
    }

    //vStudentMustPayToEndDate: every operation (the future too); the probable end is the latest of the rows' ends,
    //their group's void date, or the month after the last operation month
    [Fact]
    public void Build_MustPayToEnd_TakesAllOperationsAndTheLatestProbableEnd()
    {
        // Arrange
        Contract(1);
        Payment(1, At(9, 1), 10m);
        Charge(1, At(9, 10, 15), -30m);
        Charge(1, At(12, 10, 15), -90m);
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, At(11, 1), null));
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, null, At(12, 20)));

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Equal(-20m, row.Balance);
        Assert.Equal(110m, row.MustPayToEnd);
        Assert.Equal(At(12, 20), row.EndDate);
    }

    [Fact]
    public void Build_MustPayToEnd_OpenRow_EndsAMonthAfterTheLastOperationMonth()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, At(11, 1), null));
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, null, null));

        // Act + Assert
        Assert.Equal(At(12, 1).AddYears(1), Row(1).EndDate);
    }

    [Fact]
    public void Build_MustPayToEnd_EmptyOperationMonths_LeavesTheOpenRowWithoutAnEnd()
    {
        // Arrange
        _lastOperationMonth = null;
        Contract(1);
        Charge(1, At(9, 10, 15));
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, null, null));

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Equal(10m, row.MustPayToEnd);
        Assert.Null(row.EndDate);
    }

    [Fact]
    public void Build_MustPayToEnd_NoGroupRow_IsNull()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Null(row.MustPayToEnd);
        Assert.Null(row.EndDate);
    }

    //a contract without operations has neither a balance nor a desired day amount, so it is never in the list
    [Fact]
    public void Build_NoOperationAtAll_IsNotInTheList()
    {
        // Arrange
        Contract(1, desiredDay: 1);
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, null, null));
        Contract(2, desiredDay: 1);
        Charge(2, At(9, 10, 15));

        // Act + Assert
        AssertIds(Build(), 2);
    }

    [Fact]
    public void Build_CopiesTheContractAndTheNextLessonAndTheStopDate()
    {
        // Arrange
        Contract(1, "Beta Bob", nextPayDate: At(9, 20, 15));
        Charge(1, At(9, 20, 15));
        _nextLessons[1] = At(10, 2, 16);

        // Act
        DepositRowResponse row = Row(1);

        // Assert
        Assert.Equal(
            new DepositRowResponse(1, 11, "Beta Bob", "6.001", -10m, "555000001", "Payer Pat", "555000002",
                At(10, 2, 16), null, null, null, null, null, null, At(9, 20, 15), null, null), row);
    }

    //vStudentMustPayDate: the latest call with "must pay until"; two calls at the same time: the later id
    [Fact]
    public void Build_CrmMustPayDate_IsTheLatestCall()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));
        _crmCalls.Add(new CrmMustPayDateData(1, 1, At(9, 20, 10), At(9, 25)));
        _crmCalls.Add(new CrmMustPayDateData(2, 1, At(9, 22, 10), At(9, 30)));
        _crmCalls.Add(new CrmMustPayDateData(4, 1, At(9, 21, 10), At(10, 30)));
        Contract(2);
        Charge(2, At(9, 10, 15));
        _crmCalls.Add(new CrmMustPayDateData(6, 2, At(9, 22, 10), At(10, 5)));
        _crmCalls.Add(new CrmMustPayDateData(5, 2, At(9, 22, 10), At(10, 4)));
        //the same tie in the other load order: neither the first nor the last loaded call wins (D93)
        Contract(3);
        Charge(3, At(9, 10, 15));
        _crmCalls.Add(new CrmMustPayDateData(7, 3, At(9, 22, 10), At(10, 6)));
        _crmCalls.Add(new CrmMustPayDateData(8, 3, At(9, 22, 10), At(10, 7)));

        // Act
        DepositsResponse response = Build();

        // Assert
        Assert.Equal(At(9, 30), response.Rows.Single(r => r.StudentContractId == 1).CrmMustPayDate);
        Assert.Equal(At(10, 5), response.Rows.Single(r => r.StudentContractId == 2).CrmMustPayDate);
        Assert.Equal(At(10, 7), response.Rows.Single(r => r.StudentContractId == 3).CrmMustPayDate);
    }

    //"ფილტრი": balance ≤ maximum (the list itself takes < maximum, so 0 shows only with a desired day)
    [Fact]
    public void Build_Filter_KeepsBalancesUpToTheMaximum()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15));
        Contract(2, desiredDay: 20);
        Payment(2, At(9, 1), 10m);
        Charge(2, At(9, 10, 15)); //balance 0
        Contract(3, desiredDay: 20);
        Payment(3, At(9, 1), 10m); //balance 10
        Contract(4, desiredDay: 1);
        Charge(4, At(10, 20, 15)); //no balance until "date to"

        // Act + Assert
        AssertIds(Build(), 1, 2, 3, 4);
        AssertIds(Build(EDepositsFilter.Filter), 1, 2);
    }

    //"დარეკვის ფილტრი": has a next lesson, balance ≤ maximum, and the CRM date (empty: today) is not after today
    [Fact]
    public void Build_CallFilter_NeedsALessonADebtAndNoFutureCrmDate()
    {
        // Arrange
        foreach (int scId in new[] { 1, 2, 3, 4, 5 })
        {
            Contract(scId);
            Charge(scId, At(9, 10, 15));
            _nextLessons[scId] = At(10, 2, 15);
        }

        _nextLessons.Remove(2); //no lesson
        _crmCalls.Add(new CrmMustPayDateData(1, 3, At(9, 20), Today)); //must pay today: call
        _crmCalls.Add(new CrmMustPayDateData(2, 4, At(9, 20), Today.AddDays(1))); //tomorrow: not yet
        _crmCalls.Add(new CrmMustPayDateData(3, 5, At(9, 20), At(9, 30))); //passed: call

        // Act + Assert
        AssertIds(Build(EDepositsFilter.Call), 1, 3, 5);
    }

    [Fact]
    public void Build_CallFilter_KeepsBalancesUpToTheMaximum()
    {
        // Arrange
        Contract(1, desiredDay: 20);
        Payment(1, At(9, 1), 10m);
        _nextLessons[1] = At(10, 2, 15);
        Contract(2, desiredDay: 20);
        Payment(2, At(9, 1), 10m);
        Charge(2, At(9, 10, 15));
        _nextLessons[2] = At(10, 2, 15);

        // Act + Assert
        AssertIds(Build(EDepositsFilter.Call), 2);
    }

    //without a filter: the stored next pay date, then the next lesson (empty first, like Access); then the name
    [Fact]
    public void Build_NoFilter_OrdersByStopDateThenNextLesson()
    {
        // Arrange
        Contract(1, "Delta Dan", nextPayDate: At(9, 20));
        Contract(2, "Gamma Gia", nextPayDate: At(9, 10));
        Contract(3, "Beta Bob", nextPayDate: At(9, 20));
        Contract(4, nextPayDate: At(9, 20));
        Contract(5, "Zeta Zoe");
        foreach (int scId in new[] { 1, 2, 3, 4, 5 })
        {
            Charge(scId, At(9, 10, 15));
        }

        _nextLessons[1] = At(10, 3, 15);
        _nextLessons[3] = At(10, 2, 15);

        // Act + Assert
        AssertIds(Build(), 5, 2, 4, 3, 1);
    }

    [Fact]
    public void Build_Filters_OrderByNextLessonThenName()
    {
        // Arrange
        Contract(1, "Delta Dan", nextPayDate: At(9, 10));
        Contract(2, "Gamma Gia", nextPayDate: At(9, 20));
        Contract(3, "Beta Bob");
        Contract(4);
        foreach (int scId in new[] { 1, 2, 3, 4 })
        {
            Charge(scId, At(9, 10, 15));
            _nextLessons[scId] = At(10, 2, 15);
        }

        _nextLessons[1] = At(10, 3, 15);
        _nextLessons.Remove(4);

        // Act + Assert
        AssertIds(Build(EDepositsFilter.Filter), 4, 3, 2, 1);
        AssertIds(Build(EDepositsFilter.Call), 3, 2, 1);
    }

    //equal names: the contract id decides
    [Fact]
    public void Build_SameName_OrdersById()
    {
        // Arrange
        Contract(7);
        Contract(3);
        Charge(7, At(9, 10, 15));
        Charge(3, At(9, 10, 15));

        // Act + Assert
        AssertIds(Build(), 3, 7);
    }

    //the form footer: the shown rows' balances and four-week fees
    [Fact]
    public void Build_Totals_SumTheShownRows()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15), -30m);
        _groupStudents.Add(new DepositGroupStudentData(1, 48m, null, null));
        Contract(2);
        Charge(2, At(9, 10, 15), -20.5m);
        Contract(3, desiredDay: 1);
        Charge(3, At(10, 20, 15), -5m); //no balance until "date to"
        _groupStudents.Add(new DepositGroupStudentData(3, 60m, null, null));
        Contract(4);
        Payment(4, At(9, 1), 70m); //not shown
        _groupStudents.Add(new DepositGroupStudentData(4, 99m, null, null));

        // Act
        DepositsResponse response = Build();

        // Assert
        AssertIds(response, 1, 2, 3);
        Assert.Equal(-50.5m, response.TotalBalance);
        Assert.Equal(108m, response.TotalFourWeekFee);
    }

    [Fact]
    public void Build_NoContracts_IsEmpty()
    {
        // Act
        DepositsResponse response = Build();

        // Assert
        Assert.Empty(response.Rows);
        Assert.Equal(0m, response.TotalBalance);
        Assert.Equal(0m, response.TotalFourWeekFee);
    }

    // --- the student's account (part 20): every contract of the student, of every year

    //contract 1 of 2025-2026 (year 10) left a debt of 30, contract 2 of 2026-2027 (year 11) of the same student has a
    //payment of 50 and a charge of 10: the student's balance is +10
    private void StudentWithTwoYears()
    {
        Contract(1, studentHumanId: 100, academicYearId: 10);
        Charge(1, At(5, 10, 15), -30m);
        Contract(2, desiredDay: 15, studentHumanId: 100);
        Payment(2, At(9, 1), 50m);
        Charge(2, At(9, 10, 15), -10m);
    }

    [Fact]
    public void Build_StudentWithContractsOfTwoYears_ShowsTheBalanceOfAllOfThem()
    {
        // Arrange
        StudentWithTwoYears();

        // Act
        DepositRowResponse row = Assert.Single(Build(maximum: 20m).Rows);

        // Assert: one row, of the latest year's contract
        Assert.Equal((2, 11, 10m), (row.StudentContractId, row.AcademicYearId, row.Balance));
        //the desired day amount counts the old debt too: -(−30 + 50 − 10)
        Assert.Equal(-10m, row.DesiredDayAmount);
    }

    // the student's old debt alone shows on the new contract
    [Fact]
    public void Build_OldDebt_ShowsOnTheNewContract()
    {
        // Arrange
        Contract(1, studentHumanId: 100, academicYearId: 10);
        Charge(1, At(5, 10, 15), -30m);
        Contract(2, studentHumanId: 100);

        // Act + Assert
        DepositRowResponse row = Assert.Single(Build().Rows);
        Assert.Equal((2, -30m), (row.StudentContractId, row.Balance));
    }

    // the footer counts a student once
    [Fact]
    public void Build_StudentWithTwoContracts_IsCountedOnceInTheTotals()
    {
        // Arrange
        StudentWithTwoYears();
        _groupStudents.Add(new DepositGroupStudentData(1, 40m, At(6, 30), At(6, 30)));
        _groupStudents.Add(new DepositGroupStudentData(2, 48m, null, null));

        // Act
        DepositsResponse response = Build(maximum: 20m);

        // Assert: the ended row of the old contract is not active
        Assert.Equal((10m, 48m), (response.TotalBalance, response.TotalFourWeekFee));
    }

    // the next lesson, the CRM date and the must-pay-to-end come from every contract of the student
    [Fact]
    public void Build_TakesTheLessonsCallsAndRowsOfEveryContractOfTheStudent()
    {
        // Arrange
        StudentWithTwoYears();
        _nextLessons[1] = At(10, 12, 15);
        _nextLessons[2] = At(10, 5, 15);
        _crmCalls.Add(new CrmMustPayDateData(600, 1, At(9, 20, 10), At(9, 25)));
        _crmCalls.Add(new CrmMustPayDateData(601, 2, At(9, 21, 10), At(9, 30)));
        _groupStudents.Add(new DepositGroupStudentData(2, 48m, null, null));

        // Act
        DepositRowResponse row = Assert.Single(Build(maximum: 20m).Rows);

        // Assert
        Assert.Equal((At(10, 5, 15), At(9, 30)), (row.NextLessonDate, row.CrmMustPayDate));
        Assert.Equal(-10m, row.MustPayToEnd);
    }

    // two contracts of the same year (older data): the larger id is the student's row
    [Fact]
    public void Build_TwoContractsOfOneYear_ShowsTheLaterOne()
    {
        // Arrange
        Contract(1, studentHumanId: 100);
        Charge(1, At(9, 10, 15), -30m);
        Contract(2, studentHumanId: 100);

        // Act + Assert
        Assert.Equal(2, Assert.Single(Build().Rows).StudentContractId);
    }

    // a contract that is not in the map is its own account
    [Fact]
    public void Build_ContractWithoutStudent_IsItsOwnAccount()
    {
        // Arrange
        Contract(1);
        Charge(1, At(9, 10, 15), -30m);
        _studentByContract.Remove(1);

        // Act + Assert
        Assert.Equal(-30m, Assert.Single(Build().Rows).Balance);
    }
}
