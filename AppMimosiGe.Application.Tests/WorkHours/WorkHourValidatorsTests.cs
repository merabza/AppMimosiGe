using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours;
using AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;
using AppMimosiGe.Application.WorkHours.CreateWorkHour;
using AppMimosiGe.Application.WorkHours.EndWork;
using AppMimosiGe.Application.WorkHours.StartWork;
using AppMimosiGe.Application.WorkHours.UpdateWorkHour;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.WorkHours;

public sealed class WorkHourValidatorsTests
{
    private static readonly DateTime Start = new(2026, 9, 24, 9, 55, 12, DateTimeKind.Unspecified);

    private readonly Mock<IWorkHoursRepository> _repository = new();

    public WorkHourValidatorsTests()
    {
        _repository.Setup(r => r.EmployeeExists(15, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static DateTime Day(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static WorkHourRequest Request(int teacherContractId = 15, DateTime? start = null, DateTime? end = null)
    {
        return new WorkHourRequest { TeacherContractId = teacherContractId, WhStart = start ?? Start, WhEnd = end };
    }

    private static WorkTimeFixRequest FixRequest(int? teacherContractId = 15, int? luft = 5)
    {
        return new WorkTimeFixRequest { TeacherContractId = teacherContractId, LuftMinutes = luft };
    }

    private static string[] Codes(ValidationResult result) => [.. result.Errors.Select(e => e.ErrorCode)];

    private async Task<string[]> CreateErrorCodes(WorkHourRequest? request) =>
        Codes(await new CreateWorkHourCommandValidator(_repository.Object).ValidateAsync(
            new CreateWorkHourCommand(request)));

    private async Task<string[]> UpdateErrorCodes(WorkHourRequest? request) =>
        Codes(await new UpdateWorkHourCommandValidator(_repository.Object).ValidateAsync(
            new UpdateWorkHourCommand(5, request)));

    private async Task<string[]> StartErrorCodes(WorkTimeFixRequest? request) =>
        Codes(await new StartWorkCommandValidator(_repository.Object).ValidateAsync(new StartWorkCommand(request)));

    private async Task<string[]> EndErrorCodes(WorkTimeFixRequest? request) =>
        Codes(await new EndWorkCommandValidator(_repository.Object).ValidateAsync(new EndWorkCommand(request)));

    private static async Task<string[]> AutoGenerateErrorCodes(WorkHoursAutoGenerateRequest? request) =>
        Codes(await new AutoGenerateWorkHoursCommandValidator().ValidateAsync(
            new AutoGenerateWorkHoursCommand(request)));

    // a record without an end is a started one (the end is fixed later)
    [Fact]
    public async Task ValidRecord_HasNoErrors()
    {
        Assert.Empty(await CreateErrorCodes(Request(end: Start.AddHours(8))));
        Assert.Empty(await UpdateErrorCodes(Request()));
    }

    [Fact]
    public async Task NullRequest_CouldNotBeDecrypted()
    {
        string[] expected = [CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code];
        Assert.Equal(expected, await CreateErrorCodes(null));
        Assert.Equal(expected, await UpdateErrorCodes(null));
        Assert.Equal(expected, await StartErrorCodes(null));
        Assert.Equal(expected, await EndErrorCodes(null));
        Assert.Equal(expected, await AutoGenerateErrorCodes(null));
    }

    // the employee is a contract with a work hour group (the repository checks both)
    [Fact]
    public async Task UnknownEmployee_IsNotFound()
    {
        Assert.Equal([WorkHourErrors.EmployeeNotFound.Code], await CreateErrorCodes(Request(16)));
        Assert.Equal([WorkHourErrors.EmployeeNotFound.Code], await UpdateErrorCodes(Request(16)));
    }

    [Fact]
    public async Task EmptyStart_IsRequired()
    {
        Assert.Equal([WorkHourErrors.StartIsRequired.Code], await CreateErrorCodes(Request(start: DateTime.MinValue)));
    }

    // unlike Access, the end must be after the start; an equal end is no work either
    [Fact]
    public async Task EndNotAfterStart_IsInvalid()
    {
        Assert.Equal([WorkHourErrors.EndMustBeAfterStart.Code], await CreateErrorCodes(Request(end: Start)));
        Assert.Equal([WorkHourErrors.EndMustBeAfterStart.Code],
            await UpdateErrorCodes(Request(end: Start.AddSeconds(-1))));
        Assert.Empty(await UpdateErrorCodes(Request(end: Start.AddSeconds(1))));
    }

    [Fact]
    public async Task SeveralRecordErrors_AreAllReported()
    {
        Assert.Equal([WorkHourErrors.EmployeeNotFound.Code, WorkHourErrors.StartIsRequired.Code],
            await CreateErrorCodes(Request(16, DateTime.MinValue)));
    }

    // a luft up to 30 minutes, none, or a negative one (counted as 0) is valid
    [Theory]
    [InlineData(null)]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(30)]
    public async Task ValidFixRequest_HasNoErrors(int? luft)
    {
        Assert.Empty(await StartErrorCodes(FixRequest(luft: luft)));
        Assert.Empty(await EndErrorCodes(FixRequest(luft: luft)));
    }

    // Access: "The employee should be selected"; nothing is looked up then
    [Fact]
    public async Task NoEmployee_IsRequiredAndNotLookedUp()
    {
        Assert.Equal([WorkHourErrors.EmployeeIsRequired.Code], await StartErrorCodes(FixRequest(null)));
        Assert.Equal([WorkHourErrors.EmployeeIsRequired.Code], await EndErrorCodes(FixRequest(null)));
        _repository.Verify(r => r.EmployeeExists(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnknownFixEmployee_IsNotFound()
    {
        Assert.Equal([WorkHourErrors.EmployeeNotFound.Code], await StartErrorCodes(FixRequest(16)));
        Assert.Equal([WorkHourErrors.EmployeeNotFound.Code], await EndErrorCodes(FixRequest(16)));
    }

    // Access: "The Lupt is too big"
    [Fact]
    public async Task LuftOverThirty_IsTooBig()
    {
        Assert.Equal([WorkHourErrors.LuftIsTooBig.Code], await StartErrorCodes(FixRequest(luft: 31)));
        Assert.Equal([WorkHourErrors.EmployeeIsRequired.Code, WorkHourErrors.LuftIsTooBig.Code],
            await EndErrorCodes(FixRequest(null, 31)));
    }

    // one day is a period: both days are included
    [Fact]
    public async Task ValidPeriod_HasNoErrors()
    {
        Assert.Empty(await AutoGenerateErrorCodes(new WorkHoursAutoGenerateRequest
        {
            DateFrom = Day(9, 1), DateTo = Day(9, 30)
        }));
        Assert.Empty(await AutoGenerateErrorCodes(new WorkHoursAutoGenerateRequest
        {
            DateFrom = Day(9, 1, 18), DateTo = Day(9, 1)
        }));
    }

    // Access inserted nothing with an empty date
    [Fact]
    public async Task MissingDate_IsRequired()
    {
        Assert.Equal([WorkHourErrors.PeriodIsRequired.Code],
            await AutoGenerateErrorCodes(new WorkHoursAutoGenerateRequest { DateTo = Day(9, 30) }));
        Assert.Equal([WorkHourErrors.PeriodIsRequired.Code],
            await AutoGenerateErrorCodes(new WorkHoursAutoGenerateRequest { DateFrom = Day(9, 1) }));
        Assert.Equal([WorkHourErrors.PeriodIsRequired.Code],
            await AutoGenerateErrorCodes(new WorkHoursAutoGenerateRequest()));
    }

    [Fact]
    public async Task DateFromAfterDateTo_IsInvalid()
    {
        Assert.Equal([WorkHourErrors.PeriodIsInvalid.Code],
            await AutoGenerateErrorCodes(
                new WorkHoursAutoGenerateRequest { DateFrom = Day(9, 2), DateTo = Day(9, 1, 23) }));
    }
}
