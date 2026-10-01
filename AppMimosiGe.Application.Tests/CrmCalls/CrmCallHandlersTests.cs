using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls;
using AppMimosiGe.Application.CrmCalls.CreateCrmCall;
using AppMimosiGe.Application.CrmCalls.DeleteCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCallFormLookups;
using AppMimosiGe.Application.CrmCalls.GetCrmCallsRowsData;
using AppMimosiGe.Application.CrmCalls.GetCrmCallStudentContracts;
using AppMimosiGe.Application.CrmCalls.Models;
using AppMimosiGe.Application.CrmCalls.UpdateCrmCall;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.CrmCalls;

public sealed class CrmCallHandlersTests
{
    private static readonly DateTime CallDate = new(2026, 9, 24, 19, 48, 29, DateTimeKind.Unspecified);

    private readonly Mock<ICrmCallsRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static CrmCallRequest Request(string? callConversation = "  will pay  ", DateTime? mustPayDate = null)
    {
        return new CrmCallRequest
        {
            StudentContractId = 10,
            CallTypeId = 1,
            CallDate = CallDate,
            AnswerTypeId = 3,
            CallConversation = callConversation,
            MustPayDate = mustPayDate
        };
    }

    private CrmCall WithExistingCall()
    {
        var crmCall = new CrmCall
        {
            CcId = 5,
            StudentContractId = 20,
            CallTypeId = 2,
            CallDate = CallDate.AddDays(-1),
            AnswerTypeId = 1,
            CallConversation = "old",
            MustPayDate = CallDate.Date
        };
        _repository.Setup(r => r.GetForChange(5, It.IsAny<CancellationToken>())).ReturnsAsync(crmCall);
        return crmCall;
    }

    private Task<Result<int>> Create(CrmCallRequest request)
    {
        return new CreateCrmCallCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateCrmCallCommand(request), CancellationToken.None);
    }

    private Task<Result> Update(CrmCallRequest request)
    {
        return new UpdateCrmCallCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateCrmCallCommand(5, request), CancellationToken.None);
    }

    private Task<Result> Delete()
    {
        return new DeleteCrmCallCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteCrmCallCommand(5), CancellationToken.None);
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    private static void AssertError(Result result, Error error)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(error.Code, result.Error.Code);
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    // the call date keeps its time, the "must pay by" date is a day, the text is trimmed
    [Fact]
    public async Task Create_AddsTheCallWithTheRequestFields()
    {
        // Arrange
        CrmCall? added = null;
        _repository.Setup(r => r.Add(It.IsAny<CrmCall>())).Callback<CrmCall>(c =>
        {
            added = c;
            c.CcId = 77;
        });

        // Act
        Result<int> result = await Create(Request(mustPayDate: new DateTime(2026, 10, 8, 13, 5, 0,
            DateTimeKind.Unspecified)));

        // Assert
        Assert.Equal(77, result.Value);
        Assert.NotNull(added);
        Assert.Equal(10, added.StudentContractId);
        Assert.Equal(1, added.CallTypeId);
        Assert.Equal(CallDate, added.CallDate);
        Assert.Equal(3, added.AnswerTypeId);
        Assert.Equal("will pay", added.CallConversation);
        Assert.Equal(new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Unspecified), added.MustPayDate);
        VerifySaved(Times.Once());
    }

    // a blank conversation and no date are stored as NULL
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_BlankConversation_IsStoredAsNull(string? callConversation)
    {
        // Arrange
        CrmCall? added = null;
        _repository.Setup(r => r.Add(It.IsAny<CrmCall>())).Callback<CrmCall>(c => added = c);

        // Act
        await Create(Request(callConversation));

        // Assert
        Assert.NotNull(added);
        Assert.Null(added.CallConversation);
        Assert.Null(added.MustPayDate);
    }

    [Fact]
    public async Task Update_ChangesEveryField()
    {
        // Arrange
        CrmCall crmCall = WithExistingCall();

        // Act
        Result result = await Update(Request());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, crmCall.CcId);
        Assert.Equal(10, crmCall.StudentContractId);
        Assert.Equal(1, crmCall.CallTypeId);
        Assert.Equal(CallDate, crmCall.CallDate);
        Assert.Equal(3, crmCall.AnswerTypeId);
        Assert.Equal("will pay", crmCall.CallConversation);
        Assert.Null(crmCall.MustPayDate);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_MissingCall_IsNotFound()
    {
        // Act
        Result result = await Update(Request());

        // Assert
        AssertError(result, CrmCallErrors.CrmCallNotFound);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Delete_RemovesTheCall()
    {
        // Arrange
        CrmCall crmCall = WithExistingCall();

        // Act
        Result result = await Delete();

        // Assert
        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(crmCall), Times.Once);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_MissingCall_IsNotFound()
    {
        // Act
        Result result = await Delete();

        // Assert
        AssertError(result, CrmCallErrors.CrmCallNotFound);
        _repository.Verify(r => r.Remove(It.IsAny<CrmCall>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Get_ReturnsTheCall()
    {
        // Arrange
        var crmCall = new CrmCallResponse(5, 10, "Alpha Ann / 6.001", 11, 1, CallDate, 3, null, null);
        _repository.Setup(r => r.GetOne(5, It.IsAny<CancellationToken>())).ReturnsAsync(crmCall);

        // Act
        Result<CrmCallResponse> result =
            await new GetCrmCallQueryHandler(_repository.Object).Handle(new GetCrmCallQuery(5), CancellationToken.None);

        // Assert
        Assert.Same(crmCall, result.Value);
    }

    [Fact]
    public async Task Get_MissingCall_IsNotFound()
    {
        // Act
        Result<CrmCallResponse> result =
            await new GetCrmCallQueryHandler(_repository.Object).Handle(new GetCrmCallQuery(5), CancellationToken.None);

        // Assert
        AssertError(result, CrmCallErrors.CrmCallNotFound);
    }

    // the years (sorted by name) with the current one, the call types and the results
    [Fact]
    public async Task GetFormLookups_ReturnsTheYearsTheCurrentYearTheTypesAndTheResults()
    {
        // Arrange
        var studentContractsRepository = new Mock<IStudentContractsRepository>();
        studentContractsRepository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 11,
                AcademicYearName = "2026-2027",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            },
            new AcademicYear
            {
                AyId = 10,
                AcademicYearName = "2025-2026",
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            }
        ]);
        List<LookupItemResponse> callTypes = [new(1, "Reminder")];
        List<LookupItemResponse> answerTypes = [new(3, "Answered")];
        _repository.Setup(r => r.GetCallTypes(It.IsAny<CancellationToken>())).ReturnsAsync(callTypes);
        _repository.Setup(r => r.GetAnswerTypes(It.IsAny<CancellationToken>())).ReturnsAsync(answerTypes);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);

        // Act
        Result<CrmCallFormLookupsResponse> result = await new GetCrmCallFormLookupsQueryHandler(_repository.Object,
                studentContractsRepository.Object, timeProvider.Object)
            .Handle(new GetCrmCallFormLookupsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(11, result.Value.CurrentAcademicYearId);
        Assert.Equal([new LookupItemResponse(10, "2025-2026"), new LookupItemResponse(11, "2026-2027")],
            result.Value.AcademicYears);
        Assert.Same(callTypes, result.Value.CallTypes);
        Assert.Same(answerTypes, result.Value.AnswerTypes);
    }

    [Fact]
    public async Task GetStudentContracts_ReturnsTheContractsOfTheYear()
    {
        // Arrange
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann / 6.001")];
        _repository.Setup(r => r.GetStudentContracts(11, It.IsAny<CancellationToken>())).ReturnsAsync(contracts);

        // Act
        Result<List<LookupItemResponse>> result =
            await new GetCrmCallStudentContractsQueryHandler(_repository.Object).Handle(
                new GetCrmCallStudentContractsQuery(11), CancellationToken.None);

        // Assert
        Assert.Same(contracts, result.Value);
    }

    [Fact]
    public async Task GetRowsData_PassesTheFilter()
    {
        // Arrange
        var rows = new CrmCallsRowsDataResponse(0, 0, []);
        CrmCallsListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<CrmCallsListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<CrmCallsListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(rows);

        // Act
        Result<CrmCallsRowsDataResponse> result = await new GetCrmCallsRowsDataQueryHandler(_repository.Object).Handle(
            new GetCrmCallsRowsDataQuery(Encode(
                """{"offset":10,"rowsCount":5,"filterFields":[{"fieldName":"answerTypeId","value":"3"},{"fieldName":"dateFrom","value":"2026-09-01"}]}""")),
            CancellationToken.None);

        // Assert
        Assert.Same(rows, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(5, passed.RowsCount);
        Assert.Equal(3, passed.AnswerTypeId);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), passed.DateFrom);
    }

    // not base64 (FormatException), not JSON (JsonException), JSON null (no request)
    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
    [InlineData("bnVsbA==")]
    public async Task GetRowsData_UnreadableRequest_IsInvalid(string filterSortRequest)
    {
        // Act
        Result<CrmCallsRowsDataResponse> result =
            await new GetCrmCallsRowsDataQueryHandler(_repository.Object).Handle(
                new GetCrmCallsRowsDataQuery(filterSortRequest), CancellationToken.None);

        // Assert
        AssertError(result, CrmCallErrors.FilterSortRequestIsInvalid);
    }

    [Fact]
    public async Task GetRowsData_InvalidFilter_IsInvalidAndNotLoaded()
    {
        // Act
        Result<CrmCallsRowsDataResponse> result = await new GetCrmCallsRowsDataQueryHandler(_repository.Object).Handle(
            new GetCrmCallsRowsDataQuery(
                Encode("""{"offset":0,"rowsCount":5,"filterFields":[{"fieldName":"callTypeId","value":"x"}]}""")),
            CancellationToken.None);

        // Assert
        AssertError(result, CrmCallErrors.FilterSortRequestIsInvalid);
        _repository.Verify(r => r.GetRowsData(It.IsAny<CrmCallsListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
