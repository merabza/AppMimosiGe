using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls;
using AppMimosiGe.Application.CrmCalls.CreateCrmCall;
using AppMimosiGe.Application.CrmCalls.UpdateCrmCall;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;
using Range = Moq.Range;

namespace AppMimosiGe.Application.Tests.CrmCalls;

public sealed class CrmCallValidatorsTests
{
    private static readonly DateTime CallDate = new(2026, 9, 24, 19, 48, 29, DateTimeKind.Unspecified);

    private readonly Mock<ICrmCallsRepository> _repository = new();

    public CrmCallValidatorsTests()
    {
        _repository.Setup(r => r.StudentContractExists(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.CallTypeExists(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.AnswerTypeExists(It.IsInRange(1, 3, Range.Inclusive), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static CrmCallRequest Request(int studentContractId = 10, int callTypeId = 1, DateTime? callDate = null,
        int? answerTypeId = 3, string? callConversation = null, DateTime? mustPayDate = null)
    {
        return new CrmCallRequest
        {
            StudentContractId = studentContractId,
            CallTypeId = callTypeId,
            CallDate = callDate ?? CallDate,
            AnswerTypeId = answerTypeId,
            CallConversation = callConversation,
            MustPayDate = mustPayDate
        };
    }

    private async Task<string[]> CreateErrorCodes(CrmCallRequest? request)
    {
        ValidationResult result =
            await new CreateCrmCallCommandValidator(_repository.Object).ValidateAsync(
                new CreateCrmCallCommand(request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    private async Task<string[]> UpdateErrorCodes(CrmCallRequest? request)
    {
        ValidationResult result =
            await new UpdateCrmCallCommandValidator(_repository.Object).ValidateAsync(
                new UpdateCrmCallCommand(5, request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    // the conversation is a memo (nvarchar(max)): a long text is valid, and so are an empty one and no date
    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        Assert.Empty(await CreateErrorCodes(Request(callConversation: new string('ა', 5000),
            mustPayDate: CallDate.AddDays(7))));
        Assert.Empty(await UpdateErrorCodes(Request(answerTypeId: 1)));
    }

    [Fact]
    public async Task NullRequest_CouldNotBeDecrypted()
    {
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code], await CreateErrorCodes(null));
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code], await UpdateErrorCodes(null));
    }

    [Fact]
    public async Task MissingStudentContract_IsNotFound()
    {
        Assert.Equal([CrmCallErrors.StudentContractNotFound.Code],
            await CreateErrorCodes(Request(11)));
        Assert.Equal([CrmCallErrors.StudentContractNotFound.Code],
            await UpdateErrorCodes(Request(11)));
    }

    [Fact]
    public async Task MissingCallType_IsNotFound()
    {
        Assert.Equal([CrmCallErrors.CallTypeNotFound.Code], await CreateErrorCodes(Request(callTypeId: 2)));
    }

    [Fact]
    public async Task EmptyCallDate_IsRequired()
    {
        Assert.Equal([CrmCallErrors.CallDateIsRequired.Code],
            await CreateErrorCodes(Request(callDate: DateTime.MinValue)));
    }

    // the result was REQ in Access
    [Fact]
    public async Task NoAnswerType_IsRequiredAndNotLookedUp()
    {
        Assert.Equal([CrmCallErrors.AnswerTypeIsRequired.Code], await UpdateErrorCodes(Request(answerTypeId: null)));
        _repository.Verify(r => r.AnswerTypeExists(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingAnswerType_IsNotFound()
    {
        Assert.Equal([CrmCallErrors.AnswerTypeNotFound.Code], await CreateErrorCodes(Request(answerTypeId: 4)));
    }

    [Fact]
    public async Task SeveralErrors_AreAllReported()
    {
        Assert.Equal(
        [
            CrmCallErrors.StudentContractNotFound.Code, CrmCallErrors.CallTypeNotFound.Code,
            CrmCallErrors.CallDateIsRequired.Code, CrmCallErrors.AnswerTypeIsRequired.Code
        ], await CreateErrorCodes(Request(11, 2, DateTime.MinValue, null)));
    }
}
