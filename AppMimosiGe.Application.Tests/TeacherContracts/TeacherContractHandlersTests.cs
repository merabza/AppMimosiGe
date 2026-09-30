using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;
using AppMimosiGe.Application.TeacherContracts.Models;
using AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.TeacherContracts.TeacherContractTestData;

namespace AppMimosiGe.Application.Tests.TeacherContracts;

public sealed class TeacherContractHandlersTests
{
    private readonly Mock<ITeacherContractsRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static TeacherContract ExistingContract()
    {
        return new TeacherContract
        {
            Id = 42,
            ContractNumber = "T3.01",
            ContractDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
            TeacherHumanId = 9,
            RsCountryId = 8,
            BankAccount = "old",
            Description = "old",
            WorkHoursStart = new DateTime(1899, 12, 30, 9, 0, 0, DateTimeKind.Unspecified),
            Line = 3
        };
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    private static TimeProvider TimeProviderAt(DateTimeOffset now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
    }

    [Fact]
    public async Task Create_AddsContractAndSaves()
    {
        TeacherContract? added = null;
        _repository.Setup(r => r.Add(It.IsAny<TeacherContract>())).Callback<TeacherContract>(tc =>
        {
            added = tc;
            tc.Id = 31;
        });
        var handler = new CreateTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);
        TeacherContractRequest request = ValidRequest(workHoursStart: new TimeOnly(12, 0),
            workHoursEnd: new TimeOnly(18, 30),
            contractEndDate: new DateTime(2027, 6, 30, 15, 0, 0, DateTimeKind.Unspecified), fixedAmount: 850.5m,
            description: "  ხელფასი ");

        Result<int> result = await handler.Handle(new CreateTeacherContractCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(31, result.Value);
        Assert.NotNull(added);
        Assert.Equal("T3.01", added.ContractNumber);
        Assert.Equal(ContractDate, added.ContractDate);
        Assert.Equal(1, added.TeacherHumanId);
        Assert.Equal("GE00TB0000000000000000", added.BankAccount);
        Assert.Equal("TBCBGE22", added.BankAccountCode);
        Assert.True(added.PensionScheme);
        Assert.False(added.IndEnt);
        Assert.Equal(1, added.RsQuoteTypeId);
        Assert.Equal(2, added.RsCountryId);
        Assert.Equal(850.5m, added.FixedAmount);
        Assert.True(added.NextMonth);
        Assert.Equal("ხელფასი", added.Description);
        Assert.Equal(4, added.SalarySchemaByHoursId);
        Assert.Equal(5, added.WorkHourGroupId);
        //Access-ის დროის ველები: 1899-12-30 hh:mm (D13)
        Assert.Equal(new DateTime(1899, 12, 30, 12, 0, 0, DateTimeKind.Unspecified), added.WorkHoursStart);
        Assert.Equal(new DateTime(1899, 12, 30, 18, 30, 0, DateTimeKind.Unspecified), added.WorkHoursEnd);
        Assert.Equal(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Unspecified), added.ContractEndDate);
        Assert.Equal(0, added.Line);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_BlankTexts_AreStoredAsNull()
    {
        TeacherContract? added = null;
        _repository.Setup(r => r.Add(It.IsAny<TeacherContract>())).Callback<TeacherContract>(tc => added = tc);
        var handler = new CreateTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);

        await handler.Handle(
            new CreateTeacherContractCommand(ValidRequest(bankAccount: " ", bankAccountCode: "", description: "  ")),
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Null(added.BankAccount);
        Assert.Null(added.BankAccountCode);
        Assert.Null(added.Description);
        Assert.Null(added.WorkHoursStart);
        Assert.Null(added.WorkHoursEnd);
        Assert.Null(added.ContractEndDate);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFoundAndDoesNotSave()
    {
        var handler = new UpdateTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new UpdateTeacherContractCommand(42, ValidRequest()),
            CancellationToken.None);

        Assert.Equal(TeacherContractErrors.TeacherContractNotFound.Code, result.Error.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // Line is not on the form, so an edit keeps it
    [Fact]
    public async Task Update_ChangesTheFieldsKeepsLineAndSaves()
    {
        TeacherContract existing = ExistingContract();
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new UpdateTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(
            new UpdateTeacherContractCommand(42, ValidRequest("T3.09", bankAccount: null)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("T3.09", existing.ContractNumber);
        Assert.Equal(1, existing.TeacherHumanId);
        Assert.Equal(2, existing.RsCountryId);
        Assert.Null(existing.BankAccount);
        Assert.Null(existing.Description);
        Assert.Null(existing.WorkHoursStart);
        Assert.Equal(3, existing.Line);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        var handler = new DeleteTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteTeacherContractCommand(42), CancellationToken.None);

        Assert.Equal(TeacherContractErrors.TeacherContractNotFound.Code, result.Error.Code);
        _repository.Verify(r => r.Remove(It.IsAny<TeacherContract>()), Times.Never);
    }

    [Fact]
    public async Task Delete_InUse_ReturnsConflictAndDoesNotRemove()
    {
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingContract());
        _repository.Setup(r => r.IsInUse(42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new DeleteTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteTeacherContractCommand(42), CancellationToken.None);

        Assert.Equal(TeacherContractErrors.TeacherContractIsInUse.Code, result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _repository.Verify(r => r.Remove(It.IsAny<TeacherContract>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_NotInUse_RemovesAndSaves()
    {
        TeacherContract existing = ExistingContract();
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new DeleteTeacherContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteTeacherContractCommand(42), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(existing), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOne_NotFound_ReturnsNotFound()
    {
        var handler = new GetTeacherContractQueryHandler(_repository.Object);

        Result<TeacherContractResponse> result =
            await handler.Handle(new GetTeacherContractQuery(42), CancellationToken.None);

        Assert.Equal(TeacherContractErrors.TeacherContractNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetOne_Found_ReturnsIt()
    {
        var expected = new TeacherContractResponse(42, "T3.01", ContractDate, 1, "A B", null, null, false, false, null,
            2, 0, false, null, null, null, null, null, null);
        _repository.Setup(r => r.GetOne(42, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var handler = new GetTeacherContractQueryHandler(_repository.Object);

        Result<TeacherContractResponse> result =
            await handler.Handle(new GetTeacherContractQuery(42), CancellationToken.None);

        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task FormLookups_ReturnsTheRepositoryLists()
    {
        List<LookupItemResponse> quoteTypes = [new(1, "q")];
        List<LookupItemResponse> countries = [new(2, "c")];
        List<LookupItemResponse> schemes = [new(3, "s")];
        List<LookupItemResponse> workHourGroups = [new(4, "w")];
        _repository.Setup(r => r.GetRsQuoteTypes(It.IsAny<CancellationToken>())).ReturnsAsync(quoteTypes);
        _repository.Setup(r => r.GetRsCountries(It.IsAny<CancellationToken>())).ReturnsAsync(countries);
        _repository.Setup(r => r.GetSalarySchemes(It.IsAny<CancellationToken>())).ReturnsAsync(schemes);
        _repository.Setup(r => r.GetWorkHourGroups(It.IsAny<CancellationToken>())).ReturnsAsync(workHourGroups);
        var handler = new GetTeacherContractFormLookupsQueryHandler(_repository.Object);

        Result<TeacherContractFormLookupsResponse> result =
            await handler.Handle(new GetTeacherContractFormLookupsQuery(), CancellationToken.None);

        Assert.Same(quoteTypes, result.Value.RsQuoteTypes);
        Assert.Same(countries, result.Value.RsCountries);
        Assert.Same(schemes, result.Value.SalarySchemes);
        Assert.Same(workHourGroups, result.Value.WorkHourGroups);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
    public async Task RowsData_BrokenRequest_Fails(string filterSortRequest)
    {
        var handler = new GetTeacherContractsRowsDataQueryHandler(_repository.Object, TimeProvider.System);

        Result<TeacherContractsRowsDataResponse> result =
            await handler.Handle(new GetTeacherContractsRowsDataQuery(filterSortRequest), CancellationToken.None);

        Assert.Equal(TeacherContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task RowsData_NullJson_Fails()
    {
        var handler = new GetTeacherContractsRowsDataQueryHandler(_repository.Object, TimeProvider.System);

        Result<TeacherContractsRowsDataResponse> result =
            await handler.Handle(new GetTeacherContractsRowsDataQuery(Encode("null")), CancellationToken.None);

        Assert.Equal(TeacherContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task RowsData_InvalidFilter_FailsWithoutQueryingRepository()
    {
        var handler = new GetTeacherContractsRowsDataQueryHandler(_repository.Object, TimeProvider.System);
        string json = """{"offset":0,"rowsCount":10,"filterFields":[{"fieldName":"activeOnly","value":"x"}]}""";

        Result<TeacherContractsRowsDataResponse> result =
            await handler.Handle(new GetTeacherContractsRowsDataQuery(Encode(json)), CancellationToken.None);

        Assert.True(result.IsFailure);
        _repository.Verify(r => r.GetRowsData(It.IsAny<TeacherContractsListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // the active filter compares with today's local date
    [Fact]
    public async Task RowsData_ValidRequest_PassesTypedQueryWithTodayToRepository()
    {
        var expected = new TeacherContractsRowsDataResponse(0, 0, []);
        TeacherContractsListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<TeacherContractsListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<TeacherContractsListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(expected);
        var handler = new GetTeacherContractsRowsDataQueryHandler(_repository.Object,
            TimeProviderAt(new DateTimeOffset(2026, 9, 30, 17, 45, 0, TimeSpan.Zero)));
        string json = Uri.EscapeDataString(
            """{"offset":10,"rowsCount":10,"filterFields":[{"fieldName":"activeOnly","value":"true"},{"fieldName":"search","value":"ბერიძე"}],"sortByFields":[{"fieldName":"fixedAmount","ascending":false}]}""");

        Result<TeacherContractsRowsDataResponse> result =
            await handler.Handle(new GetTeacherContractsRowsDataQuery(Encode(json)), CancellationToken.None);

        Assert.Same(expected, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified), passed.ActiveOn);
        Assert.Equal("ბერიძე", passed.Search);
        Assert.Equal([new TeacherContractSortField(ETeacherContractSortField.FixedAmount, false)], passed.SortFields);
    }
}
