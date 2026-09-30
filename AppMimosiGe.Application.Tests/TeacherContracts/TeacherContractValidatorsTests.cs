using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;
using static AppMimosiGe.Application.Tests.TeacherContracts.TeacherContractTestData;

namespace AppMimosiGe.Application.Tests.TeacherContracts;

public sealed class TeacherContractValidatorsTests
{
    private static async Task<string[]> ErrorCodes(TeacherContractRequest request,
        Mock<ITeacherContractsRepository>? repository = null)
    {
        var validator = new TeacherContractRequestValidator((repository ?? RepositoryWhereEverythingExists()).Object);
        ValidationResult result = await validator.ValidateAsync(request);
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(workHoursStart: new TimeOnly(12, 0),
            workHoursEnd: new TimeOnly(18, 0), contractEndDate: ContractDate.AddYears(1), fixedAmount: 800,
            description: "ხელფასი")));
    }

    [Fact]
    public async Task OptionalFieldsMayBeEmpty()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(rsQuoteTypeId: null, salarySchemaByHoursId: null,
            workHourGroupId: null, bankAccount: null, bankAccountCode: null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingContractNumber_IsRequiredError(string? contractNumber)
    {
        Assert.Equal([TeacherContractErrors.ContractNumberIsRequired.Code],
            await ErrorCodes(ValidRequest(contractNumber)));
    }

    [Theory]
    [InlineData("3.01")]
    [InlineData("T301")]
    [InlineData("T3.1")]
    [InlineData("T13.01")]
    [InlineData("t3.01")]
    [InlineData("T3.011")]
    [InlineData("6.001")]
    public async Task WrongContractNumberFormat_IsFormatError(string contractNumber)
    {
        Assert.Equal([TeacherContractErrors.ContractNumberFormatIsInvalid.Code],
            await ErrorCodes(ValidRequest(contractNumber)));
    }

    [Fact]
    public async Task MissingContractDate_IsRequiredError()
    {
        var request = new TeacherContractRequest { ContractNumber = "T3.01", TeacherHumanId = 1, RsCountryId = 2 };
        Assert.Equal([TeacherContractErrors.ContractDateIsRequired.Code], await ErrorCodes(request));
    }

    [Fact]
    public async Task TooLongTexts_AreLengthErrors()
    {
        TeacherContractRequest request = ValidRequest(bankAccount: new string('1', 23),
            bankAccountCode: new string('A', 9), description: new string('ა', 256));
        Assert.Equal([
            TeacherContractErrors.BankAccountIsTooLong.Code, TeacherContractErrors.BankAccountCodeIsTooLong.Code,
            TeacherContractErrors.DescriptionIsTooLong.Code
        ], await ErrorCodes(request));
    }

    [Fact]
    public async Task TextsOfMaximumLength_AreAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(bankAccount: new string('1', 22),
            bankAccountCode: new string('A', 8), description: new string('ა', 255))));
    }

    [Fact]
    public async Task NegativeFixedAmount_IsError()
    {
        Assert.Equal([TeacherContractErrors.FixedAmountMustNotBeNegative.Code],
            await ErrorCodes(ValidRequest(fixedAmount: -0.01m)));
    }

    [Theory]
    [InlineData(18, 0, 12, 0)]
    [InlineData(12, 0, 12, 0)]
    public async Task WorkHoursStartNotBeforeEnd_IsError(int startHour, int startMinute, int endHour, int endMinute)
    {
        Assert.Equal([TeacherContractErrors.WorkHoursStartMustBeBeforeEnd.Code],
            await ErrorCodes(ValidRequest(workHoursStart: new TimeOnly(startHour, startMinute),
                workHoursEnd: new TimeOnly(endHour, endMinute))));
    }

    // only one of the two times says nothing about the order
    [Fact]
    public async Task OnlyOneWorkHoursTime_IsAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(workHoursStart: new TimeOnly(18, 0))));
        Assert.Empty(await ErrorCodes(ValidRequest(workHoursEnd: new TimeOnly(8, 0))));
    }

    [Fact]
    public async Task ContractEndDateBeforeContractDate_IsError()
    {
        Assert.Equal([TeacherContractErrors.ContractEndDateIsBeforeContractDate.Code],
            await ErrorCodes(ValidRequest(contractEndDate: ContractDate.AddDays(-1))));
    }

    [Fact]
    public async Task ContractEndDateOnContractDate_IsAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(contractEndDate: ContractDate)));
    }

    // only the day of the contract date counts, a time part coming with it does not
    [Fact]
    public async Task ContractEndDateOnTheDayOfATimedContractDate_IsAllowed()
    {
        var request = new TeacherContractRequest
        {
            ContractNumber = "T3.01",
            ContractDate = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Unspecified),
            TeacherHumanId = 1,
            RsCountryId = 2,
            ContractEndDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified)
        };

        Assert.Empty(await ErrorCodes(request));
    }

    [Fact]
    public async Task MissingReferences_AreNotFoundErrors()
    {
        var repository = new Mock<ITeacherContractsRepository>();

        Assert.Equal([
            TeacherContractErrors.TeacherNotFound.Code, TeacherContractErrors.RsQuoteTypeNotFound.Code,
            TeacherContractErrors.RsCountryNotFound.Code, TeacherContractErrors.SalarySchemeNotFound.Code,
            TeacherContractErrors.WorkHourGroupNotFound.Code
        ], await ErrorCodes(ValidRequest(), repository));
    }

    [Fact]
    public async Task CreateValidator_NullRequest_IsDecryptError()
    {
        var validator = new CreateTeacherContractCommandValidator(RepositoryWhereEverythingExists().Object);

        ValidationResult result = await validator.ValidateAsync(new CreateTeacherContractCommand(null));

        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task CreateValidator_TakenNumber_IsConflict()
    {
        Mock<ITeacherContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.ContractNumberExists("T3.01", 0, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new CreateTeacherContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new CreateTeacherContractCommand(ValidRequest()));

        Assert.Equal([TeacherContractErrors.ContractNumberAlreadyExists.Code], result.Errors.Select(e => e.ErrorCode));
    }

    // the command validators apply the field rules of the request
    [Fact]
    public async Task CreateValidator_InvalidRequestFields_AreReported()
    {
        var validator = new CreateTeacherContractCommandValidator(RepositoryWhereEverythingExists().Object);

        ValidationResult result =
            await validator.ValidateAsync(new CreateTeacherContractCommand(ValidRequest("3.01", fixedAmount: -1)));

        Assert.Equal([
            TeacherContractErrors.ContractNumberFormatIsInvalid.Code,
            TeacherContractErrors.FixedAmountMustNotBeNegative.Code
        ], result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task UpdateValidator_InvalidRequestFields_AreReported()
    {
        var validator = new UpdateTeacherContractCommandValidator(RepositoryWhereEverythingExists().Object);

        ValidationResult result = await validator.ValidateAsync(new UpdateTeacherContractCommand(7,
            ValidRequest(workHoursStart: new TimeOnly(18, 0), workHoursEnd: new TimeOnly(9, 0))));

        Assert.Equal([TeacherContractErrors.WorkHoursStartMustBeBeforeEnd.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task CreateValidator_FreeNumber_IsValid()
    {
        var validator = new CreateTeacherContractCommandValidator(RepositoryWhereEverythingExists().Object);

        ValidationResult result = await validator.ValidateAsync(new CreateTeacherContractCommand(ValidRequest()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task UpdateValidator_NullRequest_IsDecryptError()
    {
        var validator = new UpdateTeacherContractCommandValidator(RepositoryWhereEverythingExists().Object);

        ValidationResult result = await validator.ValidateAsync(new UpdateTeacherContractCommand(7, null));

        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    // the edited contract itself is excluded from the uniqueness check
    [Fact]
    public async Task UpdateValidator_ChecksNumberExceptItself()
    {
        Mock<ITeacherContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.ContractNumberExists("T3.01", 7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new UpdateTeacherContractCommandValidator(repository.Object);

        ValidationResult taken = await validator.ValidateAsync(new UpdateTeacherContractCommand(7, ValidRequest()));
        ValidationResult free = await validator.ValidateAsync(new UpdateTeacherContractCommand(8, ValidRequest()));

        Assert.Equal([TeacherContractErrors.ContractNumberAlreadyExists.Code], taken.Errors.Select(e => e.ErrorCode));
        Assert.True(free.IsValid);
    }

    // an empty number is already a required error, uniqueness is not asked
    [Fact]
    public async Task CommandValidators_EmptyNumber_DoNotCheckUniqueness()
    {
        Mock<ITeacherContractsRepository> repository = RepositoryWhereEverythingExists();

        await new CreateTeacherContractCommandValidator(repository.Object).ValidateAsync(
            new CreateTeacherContractCommand(ValidRequest("")));
        await new UpdateTeacherContractCommandValidator(repository.Object).ValidateAsync(
            new UpdateTeacherContractCommand(7, ValidRequest("")));

        repository.Verify(
            r => r.ContractNumberExists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
