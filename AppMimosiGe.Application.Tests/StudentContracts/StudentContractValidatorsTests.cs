using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.CreateStudentContract;
using AppMimosiGe.Application.StudentContracts.UpdateStudentContract;
using AppMimosiGe.Application.StudentContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation.Results;
using Moq;
using Xunit;
using static AppMimosiGe.Application.Tests.StudentContracts.StudentContractTestData;

namespace AppMimosiGe.Application.Tests.StudentContracts;

public sealed class StudentContractValidatorsTests
{
    private static async Task<string[]> ErrorCodes(StudentContractRequest request,
        Mock<IStudentContractsRepository>? repository = null)
    {
        var validator = new StudentContractRequestValidator((repository ?? RepositoryWhereEverythingExists()).Object);
        ValidationResult result = await validator.ValidateAsync(request);
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        Assert.Empty(await ErrorCodes(ValidRequest()));
    }

    [Fact]
    public async Task StudentMayBeHisOwnPayer()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(studentHumanId: 7, payerHumanId: 7)));
    }

    [Fact]
    public async Task OptionalFieldsMayBeEmpty()
    {
        StudentContractRequest request = ValidRequest(studentStatusId: null, desiredMonthlyPaymentDay: null);
        Assert.Empty(await ErrorCodes(request));
    }

    [Fact]
    public async Task MissingContractNumber_IsRequiredError()
    {
        Assert.Equal([StudentContractErrors.ContractNumberIsRequired.Code], await ErrorCodes(ValidRequest("")));
    }

    [Theory]
    [InlineData("6001")]
    [InlineData("6.01")]
    [InlineData("16.001")]
    [InlineData("a.bcd")]
    [InlineData("6.0011")]
    public async Task WrongContractNumberFormat_IsFormatError(string contractNumber)
    {
        Assert.Equal([StudentContractErrors.ContractNumberFormatIsInvalid.Code],
            await ErrorCodes(ValidRequest(contractNumber)));
    }

    [Fact]
    public async Task MissingContractDate_IsRequiredError()
    {
        var request = new StudentContractRequest
        {
            ContractNumber = "6.001",
            StudentHumanId = 1,
            PayerHumanId = 2,
            AcademicYearId = 11,
            Details = []
        };
        Assert.Equal([StudentContractErrors.ContractDateIsRequired.Code], await ErrorCodes(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(31)]
    [InlineData(-1)]
    public async Task PaymentDayOutside1To28_IsRangeError(int day)
    {
        Assert.Equal([StudentContractErrors.DesiredMonthlyPaymentDayIsOutOfRange.Code],
            await ErrorCodes(ValidRequest(desiredMonthlyPaymentDay: day)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(28)]
    public async Task PaymentDayBoundaries_AreValid(int day)
    {
        Assert.Empty(await ErrorCodes(ValidRequest(desiredMonthlyPaymentDay: day)));
    }

    [Fact]
    public async Task MissingReferencedRecords_AreReported()
    {
        var repository = new Mock<IStudentContractsRepository>();
        string[] codes = await ErrorCodes(ValidRequest(), repository);

        Assert.Equal([
            StudentContractErrors.StudentNotFound.Code, StudentContractErrors.PayerNotFound.Code,
            StudentContractErrors.AcademicYearNotFound.Code, StudentContractErrors.StudentStatusNotFound.Code,
            StudentContractErrors.CourseNotFound.Code, StudentContractErrors.GroupSizeNotFound.Code
        ], codes);
    }

    [Fact]
    public async Task PayerIsCheckedSeparatelyFromStudent()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.HumanExists(2, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.Equal([StudentContractErrors.PayerNotFound.Code], await ErrorCodes(ValidRequest(), repository));
    }

    [Theory]
    [InlineData(0f, 48, 6, "FourWeekHoursMustBePositive")]
    [InlineData(-2f, 48, 6, "FourWeekHoursMustBePositive")]
    [InlineData(8f, -1, 6, "FeeMustNotBeNegative")]
    [InlineData(8f, 48, -0.5, "FeeMustNotBeNegative")]
    public async Task InvalidDetailNumbers_AreReported(float hours, double fourWeekFee, double oneHourFee,
        string expectedCode)
    {
        StudentContractRequest request = ValidRequest(details:
        [
            Detail(fourWeekHours: hours, fourWeekFee: (decimal)fourWeekFee, oneHourFee: (decimal)oneHourFee)
        ]);

        Assert.Equal([expectedCode], await ErrorCodes(request));
    }

    [Fact]
    public async Task ZeroFees_AreAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(details: [Detail(fourWeekFee: 0, oneHourFee: 0)])));
    }

    [Fact]
    public async Task NullDetails_AreRejected()
    {
        var request = new StudentContractRequest
        {
            ContractNumber = "6.001",
            ContractDate = ValidRequest().ContractDate,
            StudentHumanId = 1,
            PayerHumanId = 2,
            AcademicYearId = 11,
            Details = null!
        };
        Assert.NotEmpty(await ErrorCodes(request));
    }

    [Fact]
    public async Task Create_NullRequest_Fails()
    {
        var validator = new CreateStudentContractCommandValidator(RepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(new CreateStudentContractCommand(null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Create_DuplicateNumberInYear_Fails()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.ContractNumberExists(11, "6.001", 0, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new CreateStudentContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new CreateStudentContractCommand(ValidRequest()));

        Assert.Equal([StudentContractErrors.ContractNumberAlreadyExists.Code], result.Errors.Select(e => e.ErrorCode));
    }

    // the command validators run the field rules of the request too
    [Fact]
    public async Task Create_InvalidRequestFields_Fail()
    {
        var validator = new CreateStudentContractCommandValidator(RepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(
            new CreateStudentContractCommand(ValidRequest(desiredMonthlyPaymentDay: 29)));
        Assert.Equal([StudentContractErrors.DesiredMonthlyPaymentDayIsOutOfRange.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task Update_InvalidRequestFields_Fail()
    {
        var validator = new UpdateStudentContractCommandValidator(RepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(
            new UpdateStudentContractCommand(42, ValidRequest(desiredMonthlyPaymentDay: 29)));
        Assert.Equal([StudentContractErrors.DesiredMonthlyPaymentDayIsOutOfRange.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task Create_UniqueNumber_Passes()
    {
        var validator = new CreateStudentContractCommandValidator(RepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(new CreateStudentContractCommand(ValidRequest()));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Create_EmptyNumber_DoesNotQueryUniqueness()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        var validator = new CreateStudentContractCommandValidator(repository.Object);

        await validator.ValidateAsync(new CreateStudentContractCommand(ValidRequest("")));

        repository.Verify(
            r => r.ContractNumberExists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ExcludesTheContractItselfFromUniqueness()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        var validator = new UpdateStudentContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new UpdateStudentContractCommand(42, ValidRequest()));

        Assert.True(result.IsValid);
        repository.Verify(r => r.ContractNumberExists(11, "6.001", 42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_DuplicateNumber_Fails()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.ContractNumberExists(11, "6.001", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var validator = new UpdateStudentContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new UpdateStudentContractCommand(42, ValidRequest()));

        Assert.Equal([StudentContractErrors.ContractNumberAlreadyExists.Code], result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task Update_NullRequest_Fails()
    {
        var validator = new UpdateStudentContractCommandValidator(RepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(new UpdateStudentContractCommand(1, null));
        Assert.False(result.IsValid);
    }

    // part 20: the balance is the student's account over every contract, so a student has one contract in a year
    [Fact]
    public async Task Create_StudentWithAContractInTheYear_Fails()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.StudentHasContract(11, 1, 0, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new CreateStudentContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new CreateStudentContractCommand(ValidRequest()));

        Assert.Equal([StudentContractErrors.StudentAlreadyHasContract.Code], result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task Update_ExcludesTheContractItselfFromTheStudentsContracts()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        var validator = new UpdateStudentContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new UpdateStudentContractCommand(42, ValidRequest()));

        Assert.True(result.IsValid);
        repository.Verify(r => r.StudentHasContract(11, 1, 42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_StudentWithAnotherContractInTheYear_Fails()
    {
        Mock<IStudentContractsRepository> repository = RepositoryWhereEverythingExists();
        repository.Setup(r => r.StudentHasContract(11, 1, 42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new UpdateStudentContractCommandValidator(repository.Object);

        ValidationResult result = await validator.ValidateAsync(new UpdateStudentContractCommand(42, ValidRequest()));

        Assert.Equal([StudentContractErrors.StudentAlreadyHasContract.Code], result.Errors.Select(e => e.ErrorCode));
    }
}
