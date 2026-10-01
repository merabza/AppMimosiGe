using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.Balances.GetBalancesFormLookups;
using AppMimosiGe.Application.Balances.GetDeposits;
using AppMimosiGe.Application.Balances.GetStatement;
using AppMimosiGe.Application.Balances.GetStatementStudentContracts;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGe.Application.Balances.RecountBalances;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class BalanceHandlersTests
{
    //21:30 UTC on 30 September is 01:30 on 1 October in Georgia: today is 1 October only in local time
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 21, 30, 0, TimeSpan.Zero);

    private readonly Mock<IBalancesRepository> _repository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public BalanceHandlersTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
        _timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.CreateCustomTimeZone("Georgia",
            TimeSpan.FromHours(4), "Georgia", "Georgia"));
    }

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static void AssertError(Result result, Error error)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(error.Code, result.Error.Code);
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    //null: every contract
    private static Func<IReadOnlyCollection<int>?, bool> Contracts(params int[] expected) =>
        ids => ids is not null && ids.SequenceEqual(expected);

    private static bool AllContracts(IReadOnlyCollection<int>? ids) => ids is null;

    private void SetUpOperations(Func<IReadOnlyCollection<int>?, bool> scIds, List<ChargeData> charges,
        List<PaymentData> payments)
    {
        _repository.Setup(r => r.GetCharges(It.Is<IReadOnlyCollection<int>?>(ids => scIds(ids)),
            It.IsAny<CancellationToken>())).ReturnsAsync(charges);
        _repository.Setup(r => r.GetPayments(It.Is<IReadOnlyCollection<int>?>(ids => scIds(ids)),
            It.IsAny<CancellationToken>())).ReturnsAsync(payments);
    }

    private Task<Result<StatementRowsDataResponse>> GetStatement(string json) =>
        new GetStatementQueryHandler(_repository.Object).Handle(new GetStatementQuery(Encode(json)),
            CancellationToken.None);

    //contract 5: payment 100 (01.09), charges of 6 (03.09 15:00, 10.09 15:00, 17.09 15:00)
    private void SetUpContractFive(Func<IReadOnlyCollection<int>?, bool> scIds)
    {
        SetUpOperations(scIds, [
            new ChargeData(31, 5, At(9, 3, 15), "English", 48m, 8f, 1f),
            new ChargeData(32, 5, At(9, 10, 15), "English", 48m, 8f, 1f),
            new ChargeData(33, 5, At(9, 17, 15), "English", 48m, 8f, 1f)
        ], [new PaymentData(7, 5, At(9, 1), "bank 7", 100m)]);
    }

    [Fact]
    public async Task GetStatement_OneContract_LoadsItsOperationsAndPagesTheStatement()
    {
        // Arrange
        SetUpContractFive(Contracts(5));
        Func<IReadOnlyCollection<int>?, bool> contractFive = Contracts(5);
        _repository.Setup(r =>
                r.GetStudentContractNames(It.Is<IReadOnlyCollection<int>>(ids => contractFive(ids)),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [5] = "Alpha Ann / 6.005" });

        // Act: rows from 02.09, the second page of two
        Result<StatementRowsDataResponse> result = await GetStatement(
            """{"offset":2,"rowsCount":2,"filterFields":[{"fieldName":"studentContractId","value":"5"},{"fieldName":"dateFrom","value":"2026-09-02"}]}""");

        // Assert
        StatementRowsDataResponse response = result.Value;
        Assert.Equal(3, response.AllRowsCount);
        Assert.Equal(2, response.Offset);
        Assert.Equal(100m, response.StartBalance);
        Assert.Equal(82m, response.EndBalance);
        StatementRowResponse row = Assert.Single(response.Rows);
        Assert.Equal(new StatementRowResponse(false, 33, 5, "Alpha Ann / 6.005", At(9, 17, 15), "English", -6m, 82m),
            row);
    }

    [Fact]
    public async Task GetStatement_AllContracts_LoadsEveryOperationAndTheNamesOfThePage()
    {
        // Arrange
        SetUpOperations(AllContracts, [
            new ChargeData(31, 5, At(9, 3, 15), "English", 48m, 8f, 1f),
            new ChargeData(40, 6, At(9, 4, 15), "Math", 100m, 12f, 1f)
        ], [new PaymentData(7, 5, At(9, 1), "bank 7", 100m), new PaymentData(8, 9, At(9, 2), null, 10m)]);
        IReadOnlyCollection<int>? namesFor = null;
        _repository.Setup(r =>
                r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<int>, CancellationToken>((ids, _) => namesFor = ids)
            .ReturnsAsync(new Dictionary<int, string> { [5] = "Alpha Ann / 6.005" });

        // Act
        Result<StatementRowsDataResponse> result = await GetStatement("""{"offset":0,"rowsCount":3}""");

        // Assert: payment 7 of 5, payment 8 of 9, charge 31 of 5 on the page; contract 6 only on the next page
        StatementRowsDataResponse response = result.Value;
        Assert.Equal(4, response.AllRowsCount);
        Assert.Equal([(true, 7), (true, 8), (false, 31)], response.Rows.Select(r => (r.IsPayment, r.Id)));
        Assert.Equal([110m, 104m], response.Rows.Skip(1).Select(r => r.RunningTotal));
        Assert.Equal([5, 9], namesFor);
        //a name the repository did not find stays empty
        Assert.Equal(["Alpha Ann / 6.005", "", "Alpha Ann / 6.005"], response.Rows.Select(r => r.StudentName));
        //the amounts are rounded like the totals
        Assert.Equal(-8.3333m, (await GetStatement("""{"offset":3,"rowsCount":3}""")).Value.Rows[0].Amount);
    }

    //the page may be gone after a filter change: then the last page shows
    [Fact]
    public async Task GetStatement_OffsetAfterTheLastRow_ShowsTheLastPage()
    {
        // Arrange
        SetUpContractFive(AllContracts);
        _repository.Setup(r =>
                r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        Result<StatementRowsDataResponse> result = await GetStatement("""{"offset":40,"rowsCount":3}""");

        // Assert
        Assert.Equal(3, result.Value.Offset);
        Assert.Equal(33, Assert.Single(result.Value.Rows).Id);
    }

    [Fact]
    public async Task GetStatement_NoOperations_IsAnEmptyFirstPage()
    {
        // Arrange
        SetUpOperations(AllContracts, [], []);
        _repository.Setup(r =>
                r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        Result<StatementRowsDataResponse> result = await GetStatement("""{"offset":20,"rowsCount":10}""");

        // Assert
        Assert.Equal(0, result.Value.AllRowsCount);
        Assert.Equal(20, result.Value.Offset);
        Assert.Equal(0m, result.Value.StartBalance);
        Assert.Equal(0m, result.Value.EndBalance);
        Assert.Empty(result.Value.Rows);
    }

    // not base64 (FormatException), not JSON (JsonException), JSON null (no request)
    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
    [InlineData("bnVsbA==")]
    public async Task GetStatement_UnreadableRequest_IsInvalid(string filterSortRequest)
    {
        // Act
        Result<StatementRowsDataResponse> result = await new GetStatementQueryHandler(_repository.Object).Handle(
            new GetStatementQuery(filterSortRequest), CancellationToken.None);

        // Assert
        AssertError(result, BalanceErrors.FilterSortRequestIsInvalid);
    }

    [Fact]
    public async Task GetStatement_SortedRequest_IsInvalidAndNotLoaded()
    {
        // Act
        Result<StatementRowsDataResponse> result = await GetStatement(
            """{"offset":0,"rowsCount":5,"sortByFields":[{"fieldName":"amount","ascending":true}]}""");

        // Assert
        AssertError(result, BalanceErrors.FilterSortRequestIsInvalid);
        _repository.Verify(r => r.GetCharges(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFormLookups_ReturnsTheYearsByNameAndTheCurrentOne()
    {
        // Arrange
        var studentContractsRepository = new Mock<IStudentContractsRepository>();
        studentContractsRepository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = At(9, 1), FinishDate = At(9, 1).AddYears(1)
            },
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = At(9, 1).AddYears(-1), FinishDate = At(9, 1)
            }
        ]);

        // Act
        Result<BalancesFormLookupsResponse> result =
            await new GetBalancesFormLookupsQueryHandler(studentContractsRepository.Object, _timeProvider.Object)
                .Handle(new GetBalancesFormLookupsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(11, result.Value.CurrentAcademicYearId);
        Assert.Equal([new LookupItemResponse(10, "2025-2026"), new LookupItemResponse(11, "2026-2027")],
            result.Value.AcademicYears);
    }

    //on 30 September UTC the local date is already 1 October, the first day of 2026-2027
    [Fact]
    public async Task GetFormLookups_UsesTheLocalDate()
    {
        // Arrange
        var studentContractsRepository = new Mock<IStudentContractsRepository>();
        studentContractsRepository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = At(10, 1).AddYears(-1), FinishDate = At(10, 1)
            },
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = At(10, 1), FinishDate = At(10, 1).AddYears(1)
            }
        ]);

        // Act
        Result<BalancesFormLookupsResponse> result =
            await new GetBalancesFormLookupsQueryHandler(studentContractsRepository.Object, _timeProvider.Object)
                .Handle(new GetBalancesFormLookupsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(11, result.Value.CurrentAcademicYearId);
    }

    [Fact]
    public async Task GetStatementStudentContracts_ReturnsTheContractsOfTheYear()
    {
        // Arrange
        var paymentsRepository = new Mock<IPaymentsRepository>();
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann 6.001")];
        paymentsRepository.Setup(r => r.GetStudentContracts(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contracts);

        // Act
        Result<List<LookupItemResponse>> result =
            await new GetStatementStudentContractsQueryHandler(paymentsRepository.Object).Handle(
                new GetStatementStudentContractsQuery(11), CancellationToken.None);

        // Assert
        Assert.Same(contracts, result.Value);
    }

    //contract 1: a debt of 10 without a next lesson; contract 2: +10 with the desired day 30 (on 1 October local
    //time that is 30 October; on 30 September UTC it would be that day)
    private void SetUpDeposits(int? academicYearId)
    {
        _repository.Setup(r => r.GetDepositContracts(academicYearId, It.IsAny<CancellationToken>())).ReturnsAsync([
            new DepositContractData(1, 11, "Alpha Ann", "6.001", null, "Payer Pat", null, null, null),
            new DepositContractData(2, 11, "Beta Bob", "6.002", null, "Payer Pat", null, 30, null)
        ]);
        Func<IReadOnlyCollection<int>?, bool> bothContracts = Contracts(1, 2);
        SetUpOperations(bothContracts,
            [new ChargeData(31, 1, At(9, 3, 15), "English", 80m, 8f, 1f)],
            [new PaymentData(7, 2, At(9, 1), null, 10m)]);
        _repository.Setup(r => r.GetNextLessonDates(It.Is<IReadOnlyCollection<int>>(ids => bothContracts(ids)),
            At(10, 1), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetCrmMustPayDates(It.Is<IReadOnlyCollection<int>>(ids => bothContracts(ids)),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetGroupStudents(It.Is<IReadOnlyCollection<int>>(ids => bothContracts(ids)),
            It.IsAny<CancellationToken>())).ReturnsAsync([new DepositGroupStudentData(1, 80m, null, null)]);
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>())).ReturnsAsync(At(11, 1));
    }

    private Task<Result<DepositsResponse>> GetDeposits(string? filter, int? academicYearId = 11) =>
        new GetDepositsQueryHandler(_repository.Object, _timeProvider.Object).Handle(
            new GetDepositsQuery(academicYearId, 0m, At(10, 6), filter), CancellationToken.None);

    [Fact]
    public async Task GetDeposits_BuildsTheListFromTheContractsOfTheYear()
    {
        // Arrange
        SetUpDeposits(11);

        // Act
        Result<DepositsResponse> result = await GetDeposits(null);

        // Assert
        DepositsResponse response = result.Value;
        Assert.Equal([1, 2], response.Rows.Select(r => r.StudentContractId));
        Assert.Equal(-10m, response.Rows[0].Balance);
        Assert.Equal(80m, response.Rows[0].FourWeekFee);
        Assert.Equal(10m, response.Rows[0].MustPayToEnd);
        Assert.Equal(At(12, 1), response.Rows[0].EndDate);
        Assert.Equal(At(10, 30), response.Rows[1].DesiredNextPayDate);
        Assert.Equal(-10m, response.Rows[1].DesiredDayAmount);
        Assert.Equal(0m, response.TotalBalance);
        Assert.Equal(80m, response.TotalFourWeekFee);
    }

    [Fact]
    public async Task GetDeposits_AllYears_PassesNoYear()
    {
        // Arrange
        SetUpDeposits(null);

        // Act
        Result<DepositsResponse> result = await GetDeposits(null, null);

        // Assert
        Assert.Equal(2, result.Value.Rows.Count);
    }

    [Theory]
    [InlineData(null, "1,2")]
    [InlineData("", "1,2")]
    [InlineData(" ", "1,2")]
    [InlineData("none", "1,2")]
    [InlineData("filter", "1")]
    [InlineData("Filter", "1")]
    [InlineData("CALL", "")]
    public async Task GetDeposits_AppliesTheFilterByName(string? filter, string expected)
    {
        // Arrange
        SetUpDeposits(11);

        // Act
        Result<DepositsResponse> result = await GetDeposits(filter);

        // Assert
        Assert.Equal(expected, string.Join(",", result.Value.Rows.Select(r => r.StudentContractId)));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("filter,call")]
    public async Task GetDeposits_UnknownFilter_IsInvalidAndNotLoaded(string filter)
    {
        // Act
        Result<DepositsResponse> result = await GetDeposits(filter);

        // Assert
        AssertError(result, BalanceErrors.DepositsFilterIsInvalid);
        _repository.Verify(r => r.GetDepositContracts(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GroupLessonsGenerationResponse GroupResult(int grpId, int created = 0, int updated = 0,
        int deleted = 0, int added = 0, int updatedStudents = 0, int deletedStudents = 0, int errors = 0)
    {
        return new GroupLessonsGenerationResponse(grpId, $"G{grpId}", created, updated, deleted, added,
            updatedStudents, deletedStudents, 0,
            [.. Enumerable.Range(1, errors).Select(i => new LessonGeneratorErrorResponse(i, "error", null, null))],
            []);
    }

    private Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>> Generator(
        Result<LessonsGenerationResponse> result)
    {
        var generator = new Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>>();
        generator.Setup(g => g.Handle(It.IsAny<GenerateGroupsLessonsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return generator;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recount_GeneratesTheLessonsThenRecountsTheNextPayDates(bool onlyDirty)
    {
        // Arrange: groups 1 (no change), 2..7 (one kind of change each), 8 (only errors)
        var generator = new Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>>();
        var studentContract = new StudentContract { ScId = 5, ContractNumber = "6.005", DirtyNextPayDate = true };
        var calls = new List<string>();
        generator.Setup(g => g.Handle(It.IsAny<GenerateGroupsLessonsCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("lessons")).ReturnsAsync(new LessonsGenerationResponse(false, At(11, 30), 0, [
                GroupResult(1), GroupResult(2, 1), GroupResult(3, updated: 1), GroupResult(4, deleted: 1),
                GroupResult(5, added: 1), GroupResult(6, updatedStudents: 1), GroupResult(7, deletedStudents: 1),
                GroupResult(8, errors: 2)
            ]));
        _repository.Setup(r => r.GetStudentContractsForRecount(onlyDirty, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("contracts")).ReturnsAsync([studentContract]);
        SetUpOperations(Contracts(5), [new ChargeData(31, 5, At(9, 3, 15), "English", 8m, 8f, 1f)], []);

        // Act
        Result<BalancesRecountResponse> result =
            await new RecountBalancesCommandHandler(generator.Object, _repository.Object, _unitOfWork.Object,
                _timeProvider.Object).Handle(new RecountBalancesCommand(onlyDirty), CancellationToken.None);

        // Assert
        Assert.Equal(new BalancesRecountResponse(8, 6, 2, 1, 1), result.Value);
        Assert.Equal(["lessons", "contracts"], calls);
        generator.Verify(g => g.Handle(new GenerateGroupsLessonsCommand(onlyDirty, false), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(At(9, 3, 15), studentContract.NextPayDate);
        Assert.False(studentContract.DirtyNextPayDate);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recount_GeneratorFails_ReturnsItsErrorWithoutTheNextPayDates()
    {
        // Arrange
        var error = Error.Problem("GeneratorFailed", "failed");
        Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>> generator =
            Generator(Result.Failure<LessonsGenerationResponse>(error));

        // Act
        Result<BalancesRecountResponse> result =
            await new RecountBalancesCommandHandler(generator.Object, _repository.Object, _unitOfWork.Object,
                _timeProvider.Object).Handle(new RecountBalancesCommand(true), CancellationToken.None);

        // Assert
        AssertError(result, error);
        _repository.Verify(r => r.GetStudentContractsForRecount(It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
