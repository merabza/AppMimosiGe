using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary;
using AppMimosiGe.Application.Salary.CreateSalaryHeader;
using AppMimosiGe.Application.Salary.CreateSalaryPart;
using AppMimosiGe.Application.Salary.UpdateSalaryHeader;
using AppMimosiGe.Application.Salary.UpdateSalaryPart;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.Salary;

public sealed class SalaryValidatorsTests
{
    private static readonly DateTime Fifth = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<ISalaryRepository> _repository = new();

    public SalaryValidatorsTests()
    {
        _repository.Setup(r => r.EmployeeExists(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static string[] Codes(ValidationResult result) => [.. result.Errors.Select(e => e.ErrorCode)];

    private static SalaryHeaderRequest Header(DateTime? charge = null, DateTime? transfer = null) =>
        new() { ShChargeDate = charge ?? Fifth, ShTransferDate = transfer ?? Fifth };

    private static SalaryPartRequest Part(int teacherContractId = 7, int? typeId = 3) =>
        new() { TeacherContractId = teacherContractId, SalaryPartTypeId = typeId, SpAmount = 5m };

    [Fact]
    public async Task HeaderValidators_ValidRequest_HaveNoErrors()
    {
        Assert.Empty(Codes(await new CreateSalaryHeaderCommandValidator().ValidateAsync(
            new CreateSalaryHeaderCommand(Header()))));
        Assert.Empty(Codes(await new UpdateSalaryHeaderCommandValidator().ValidateAsync(
            new UpdateSalaryHeaderCommand(1, Header()))));
    }

    [Fact]
    public async Task HeaderValidators_EmptyDates_ReturnBothErrors()
    {
        // Arrange
        SalaryHeaderRequest request = Header(DateTime.MinValue, DateTime.MinValue);
        string[] expected = [SalaryErrors.ChargeDateIsRequired.Code, SalaryErrors.TransferDateIsRequired.Code];

        // Act + Assert
        Assert.Equal(expected,
            Codes(await new CreateSalaryHeaderCommandValidator().ValidateAsync(new CreateSalaryHeaderCommand(request))));
        Assert.Equal(expected,
            Codes(await new UpdateSalaryHeaderCommandValidator().ValidateAsync(
                new UpdateSalaryHeaderCommand(1, request))));
    }

    [Fact]
    public async Task HeaderValidators_NoRequest_ReturnTheDecryptionError()
    {
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            Codes(await new CreateSalaryHeaderCommandValidator().ValidateAsync(new CreateSalaryHeaderCommand(null))));
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            Codes(await new UpdateSalaryHeaderCommandValidator().ValidateAsync(
                new UpdateSalaryHeaderCommand(1, null))));
    }

    [Fact]
    public async Task PartValidators_ValidRequest_HaveNoErrors()
    {
        Assert.Empty(Codes(await new CreateSalaryPartCommandValidator(_repository.Object).ValidateAsync(
            new CreateSalaryPartCommand(1, Part()))));
        Assert.Empty(Codes(await new UpdateSalaryPartCommandValidator(_repository.Object).ValidateAsync(
            new UpdateSalaryPartCommand(1, Part()))));
    }

    [Fact]
    public async Task PartValidators_UnknownEmployeeAndNoType_ReturnBothErrors()
    {
        // Arrange
        SalaryPartRequest request = Part(8, null);
        string[] expected = [SalaryErrors.EmployeeNotFound.Code, SalaryErrors.PartTypeIsRequired.Code];

        // Act + Assert
        Assert.Equal(expected,
            Codes(await new CreateSalaryPartCommandValidator(_repository.Object).ValidateAsync(
                new CreateSalaryPartCommand(1, request))));
        Assert.Equal(expected,
            Codes(await new UpdateSalaryPartCommandValidator(_repository.Object).ValidateAsync(
                new UpdateSalaryPartCommand(1, request))));
    }

    [Fact]
    public async Task PartValidators_NoRequest_ReturnTheDecryptionError()
    {
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            Codes(await new CreateSalaryPartCommandValidator(_repository.Object).ValidateAsync(
                new CreateSalaryPartCommand(1, null))));
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            Codes(await new UpdateSalaryPartCommandValidator(_repository.Object).ValidateAsync(
                new UpdateSalaryPartCommand(1, null))));
    }
}
