using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours;
using AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;
using AppMimosiGe.Application.WorkHours.CreateWorkHour;
using AppMimosiGe.Application.WorkHours.DeleteWorkHour;
using AppMimosiGe.Application.WorkHours.EndWork;
using AppMimosiGe.Application.WorkHours.GetWorkHour;
using AppMimosiGe.Application.WorkHours.GetWorkHourFormLookups;
using AppMimosiGe.Application.WorkHours.GetWorkHoursRowsData;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGe.Application.WorkHours.StartWork;
using AppMimosiGe.Application.WorkHours.UpdateWorkHour;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.WorkHours;

public sealed class WorkHourHandlersTests
{
    //21:30:15.9 UTC on 30 September is 01:30:15 on 1 October in Georgia: today is 1 October only in local time
    private static readonly DateTimeOffset UtcNow =
        new DateTimeOffset(2026, 9, 30, 21, 30, 15, TimeSpan.Zero).AddMilliseconds(900);

    private static readonly DateTime LocalNow = At(10, 1, 1, 30, 15);

    private readonly Mock<IWorkHoursRepository> _repository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public WorkHourHandlersTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(UtcNow);
        _timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.CreateCustomTimeZone("Georgia",
            TimeSpan.FromHours(4), "Georgia", "Georgia"));
    }

    private static DateTime At(int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(2026, month, day, hour, minute, second, DateTimeKind.Unspecified);

    private static WorkHourEmployee Employee(DateTime? contractDate = null, DateTime? contractEndDate = null) =>
        new(15, "Alpha Ann / T3.10", contractDate ?? At(9, 1), contractEndDate, null, null);

    private static WorkHourRequest Request() =>
        new() { TeacherContractId = 15, WhStart = At(9, 24, 9, 55, 12), WhEnd = At(9, 24, 18, 5) };

    private static WorkTimeFixRequest FixRequest(int? luft = 5) => new() { TeacherContractId = 15, LuftMinutes = luft };

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    private static void AssertError(Result result, Error error)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(error.Code, result.Error.Code);
    }

    private void WithEmployee(WorkHourEmployee? employee)
    {
        _repository.Setup(r => r.GetEmployee(15, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
    }

    private WorkHour WithExistingRecord()
    {
        var workHour = new WorkHour { WhId = 5, TeacherContractId = 1, WhStart = At(9, 1, 8), WhEnd = At(9, 1, 9) };
        _repository.Setup(r => r.GetForChange(5, It.IsAny<CancellationToken>())).ReturnsAsync(workHour);
        return workHour;
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    private Task<Result<WorkHourResponse>> Start(WorkTimeFixRequest request)
    {
        return new StartWorkCommandHandler(_repository.Object, _unitOfWork.Object, _timeProvider.Object).Handle(
            new StartWorkCommand(request), CancellationToken.None);
    }

    private Task<Result<WorkHourResponse>> End(WorkTimeFixRequest request)
    {
        return new EndWorkCommandHandler(_repository.Object, _unitOfWork.Object, _timeProvider.Object).Handle(
            new EndWorkCommand(request), CancellationToken.None);
    }

    private Task<Result<WorkHoursAutoGenerateResponse>> AutoGenerate(DateTime dateFrom, DateTime dateTo)
    {
        return new AutoGenerateWorkHoursCommandHandler(_repository.Object, _unitOfWork.Object, _timeProvider.Object)
            .Handle(
                new AutoGenerateWorkHoursCommand(
                    new WorkHoursAutoGenerateRequest { DateFrom = dateFrom, DateTo = dateTo }), CancellationToken.None);
    }

    [Fact]
    public async Task GetRowsData_PassesTheParsedQuery()
    {
        // Arrange
        var rows = new WorkHoursRowsDataResponse(0, 0, [], []);
        WorkHoursListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<WorkHoursListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<WorkHoursListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(rows);

        // Act
        Result<WorkHoursRowsDataResponse> result =
            await new GetWorkHoursRowsDataQueryHandler(_repository.Object).Handle(
                new GetWorkHoursRowsDataQuery(Encode(
                    """{"offset":10,"rowsCount":5,"filterFields":[{"fieldName":"teacherContractId","value":"15"}]}""")),
                CancellationToken.None);

        // Assert
        Assert.Same(rows, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(5, passed.RowsCount);
        Assert.Equal(15, passed.TeacherContractId);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("bnVsbA==")]
    public async Task GetRowsData_UnreadableRequest_IsInvalid(string filterSortRequest)
    {
        // Act
        Result<WorkHoursRowsDataResponse> result =
            await new GetWorkHoursRowsDataQueryHandler(_repository.Object).Handle(
                new GetWorkHoursRowsDataQuery(filterSortRequest), CancellationToken.None);

        // Assert
        AssertError(result, WorkHourErrors.FilterSortRequestIsInvalid);
    }

    [Fact]
    public async Task GetRowsData_InvalidFilter_IsInvalidAndNotLoaded()
    {
        // Act
        Result<WorkHoursRowsDataResponse> result =
            await new GetWorkHoursRowsDataQueryHandler(_repository.Object).Handle(
                new GetWorkHoursRowsDataQuery(
                    Encode("""{"offset":0,"rowsCount":5,"filterFields":[{"fieldName":"dateFrom","value":"x"}]}""")),
                CancellationToken.None);

        // Assert
        AssertError(result, WorkHourErrors.FilterSortRequestIsInvalid);
        _repository.Verify(r => r.GetRowsData(It.IsAny<WorkHoursListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFormLookups_ReturnsTheEmployees()
    {
        // Arrange
        List<LookupItemResponse> employees = [new(15, "Alpha Ann / T3.10")];
        _repository.Setup(r => r.GetEmployeeLookups(It.IsAny<CancellationToken>())).ReturnsAsync(employees);

        // Act
        Result<WorkHourFormLookupsResponse> result =
            await new GetWorkHourFormLookupsQueryHandler(_repository.Object).Handle(new GetWorkHourFormLookupsQuery(),
                CancellationToken.None);

        // Assert
        Assert.Same(employees, result.Value.Employees);
    }

    [Fact]
    public async Task GetOne_ReturnsTheRecordOrNotFound()
    {
        // Arrange
        var workHour = new WorkHourResponse(5, 15, "Alpha Ann / T3.10", At(9, 24, 9), null);
        _repository.Setup(r => r.GetOne(5, It.IsAny<CancellationToken>())).ReturnsAsync(workHour);
        var handler = new GetWorkHourQueryHandler(_repository.Object);

        // Act
        Result<WorkHourResponse> found = await handler.Handle(new GetWorkHourQuery(5), CancellationToken.None);
        Result<WorkHourResponse> missing = await handler.Handle(new GetWorkHourQuery(6), CancellationToken.None);

        // Assert
        Assert.Same(workHour, found.Value);
        AssertError(missing, WorkHourErrors.WorkHourNotFound);
    }

    [Fact]
    public async Task Create_AddsTheRecordWithTheRequestFields()
    {
        // Arrange
        WorkHour? added = null;
        _repository.Setup(r => r.Add(It.IsAny<WorkHour>())).Callback<WorkHour>(w =>
        {
            added = w;
            w.WhId = 77;
        });

        // Act
        Result<int> result = await new CreateWorkHourCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new CreateWorkHourCommand(Request()), CancellationToken.None);

        // Assert
        Assert.Equal(77, result.Value);
        Assert.NotNull(added);
        Assert.Equal(15, added.TeacherContractId);
        Assert.Equal(At(9, 24, 9, 55, 12), added.WhStart);
        Assert.Equal(At(9, 24, 18, 5), added.WhEnd);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_ChangesTheRecord()
    {
        // Arrange
        WorkHour workHour = WithExistingRecord();

        // Act
        Result result = await new UpdateWorkHourCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateWorkHourCommand(5, Request()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(15, workHour.TeacherContractId);
        Assert.Equal(At(9, 24, 9, 55, 12), workHour.WhStart);
        Assert.Equal(At(9, 24, 18, 5), workHour.WhEnd);
        VerifySaved(Times.Once());
    }

    // clearing the end makes the record a started one again
    [Fact]
    public async Task Update_WithoutEnd_ClearsTheEnd()
    {
        // Arrange
        WorkHour workHour = WithExistingRecord();

        // Act
        await new UpdateWorkHourCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateWorkHourCommand(5, new WorkHourRequest { TeacherContractId = 1, WhStart = At(9, 1, 8) }),
            CancellationToken.None);

        // Assert
        Assert.Null(workHour.WhEnd);
    }

    [Fact]
    public async Task Update_MissingRecord_IsNotFoundAndNotSaved()
    {
        // Act
        Result result = await new UpdateWorkHourCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateWorkHourCommand(6, Request()), CancellationToken.None);

        // Assert
        AssertError(result, WorkHourErrors.WorkHourNotFound);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Delete_RemovesTheRecord()
    {
        // Arrange
        WorkHour workHour = WithExistingRecord();

        // Act
        Result result = await new DeleteWorkHourCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteWorkHourCommand(5), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(workHour));
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_MissingRecord_IsNotFoundAndNotSaved()
    {
        // Act
        Result result = await new DeleteWorkHourCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new DeleteWorkHourCommand(6), CancellationToken.None);

        // Assert
        AssertError(result, WorkHourErrors.WorkHourNotFound);
        _repository.Verify(r => r.Remove(It.IsAny<WorkHour>()), Times.Never);
        VerifySaved(Times.Never());
    }

    // the start is the server's local time (to the second) minus the luft; "today" is the local day
    [Fact]
    public async Task Start_AddsARecordStartingTheLuftBeforeNow()
    {
        // Arrange
        WithEmployee(Employee());
        WorkHour? added = null;
        _repository.Setup(r => r.Add(It.IsAny<WorkHour>())).Callback<WorkHour>(w =>
        {
            added = w;
            w.WhId = 77;
        });

        // Act
        Result<WorkHourResponse> result = await Start(FixRequest());

        // Assert
        Assert.Equal(new WorkHourResponse(77, 15, "Alpha Ann / T3.10", At(10, 1, 1, 25, 15), null), result.Value);
        Assert.NotNull(added);
        Assert.Equal(15, added.TeacherContractId);
        Assert.Equal(At(10, 1, 1, 25, 15), added.WhStart);
        Assert.Null(added.WhEnd);
        _repository.Verify(r => r.HasRecordOnDay(15, At(10, 1), It.IsAny<CancellationToken>()));
        VerifySaved(Times.Once());
    }

    // no luft, or a negative one, starts at the very moment
    [Theory]
    [InlineData(null)]
    [InlineData(-5)]
    public async Task Start_NoOrNegativeLuft_StartsNow(int? luft)
    {
        // Arrange
        WithEmployee(Employee());

        // Act
        Result<WorkHourResponse> result = await Start(FixRequest(luft));

        // Assert
        Assert.Equal(LocalNow, result.Value.WhStart);
    }

    // Access: "Work hours record for today is exists and can not be changed"
    [Fact]
    public async Task Start_RecordOfToday_IsAConflict()
    {
        // Arrange
        WithEmployee(Employee());
        _repository.Setup(r => r.HasRecordOnDay(15, At(10, 1), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        Result<WorkHourResponse> result = await Start(FixRequest());

        // Assert
        AssertError(result, WorkHourErrors.TodayRecordExists);
        _repository.Verify(r => r.Add(It.IsAny<WorkHour>()), Times.Never);
        VerifySaved(Times.Never());
    }

    // unlike Access, the contract must be in force today: not yet started, ended yesterday; the end day itself counts
    [Theory]
    [InlineData(10, 2, null)]
    [InlineData(9, 1, 30)]
    public async Task Start_ContractNotInForceToday_IsAConflict(int startMonth, int startDay, int? endDay)
    {
        // Arrange
        WithEmployee(Employee(At(startMonth, startDay), endDay is null ? null : At(9, endDay.Value)));

        // Act
        Result<WorkHourResponse> result = await Start(FixRequest());

        // Assert
        AssertError(result, WorkHourErrors.ContractIsNotActive);
        _repository.Verify(r => r.Add(It.IsAny<WorkHour>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Start_ContractEndingToday_StartsTheRecord()
    {
        // Arrange
        WithEmployee(Employee(At(10, 1), At(10, 1)));

        // Act
        Result<WorkHourResponse> result = await Start(FixRequest());

        // Assert
        Assert.True(result.IsSuccess);
    }

    // the validator found the employee, but it is gone (or lost its group) by now
    [Fact]
    public async Task Start_EmployeeGone_IsNotFound()
    {
        // Arrange
        WithEmployee(null);

        // Act
        Result<WorkHourResponse> result = await Start(FixRequest());

        // Assert
        AssertError(result, WorkHourErrors.EmployeeNotFound);
        VerifySaved(Times.Never());
    }

    // the end is now plus the luft on the latest record started today; an end already fixed is overwritten (Access)
    [Fact]
    public async Task End_SetsTheEndOfTodaysRecordTheLuftAfterNow()
    {
        // Arrange
        WithEmployee(Employee());
        var workHour = new WorkHour
        {
            WhId = 7, TeacherContractId = 15, WhStart = At(10, 1, 0, 5), WhEnd = At(10, 1, 1)
        };
        _repository.Setup(r => r.GetLastRecordOfDayForChange(15, At(10, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workHour);

        // Act
        Result<WorkHourResponse> result = await End(FixRequest(10));

        // Assert
        Assert.Equal(new WorkHourResponse(7, 15, "Alpha Ann / T3.10", At(10, 1, 0, 5), At(10, 1, 1, 40, 15)),
            result.Value);
        Assert.Equal(At(10, 1, 1, 40, 15), workHour.WhEnd);
        VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-5)]
    public async Task End_NoOrNegativeLuft_EndsNow(int? luft)
    {
        // Arrange
        WithEmployee(Employee());
        var workHour = new WorkHour { WhId = 7, TeacherContractId = 15, WhStart = At(10, 1, 0, 5) };
        _repository.Setup(r => r.GetLastRecordOfDayForChange(15, At(10, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workHour);

        // Act
        await End(FixRequest(luft));

        // Assert
        Assert.Equal(LocalNow, workHour.WhEnd);
    }

    // Access: "Work hours record for today is not exists and can not be created"
    [Fact]
    public async Task End_WithoutRecordOfToday_IsAConflict()
    {
        // Arrange
        WithEmployee(Employee());

        // Act
        Result<WorkHourResponse> result = await End(FixRequest());

        // Assert
        AssertError(result, WorkHourErrors.TodayRecordNotFound);
        VerifySaved(Times.Never());
    }

    // a start moved by hand after the end would be: the end must be after the start, an equal one too
    [Theory]
    [InlineData(1, 35, 15)]
    [InlineData(2, 0, 0)]
    public async Task End_NotAfterTheStart_IsInvalid(int hour, int minute, int second)
    {
        // Arrange
        WithEmployee(Employee());
        var workHour = new WorkHour { WhId = 7, TeacherContractId = 15, WhStart = At(10, 1, hour, minute, second) };
        _repository.Setup(r => r.GetLastRecordOfDayForChange(15, At(10, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workHour);

        // Act
        Result<WorkHourResponse> result = await End(FixRequest());

        // Assert
        AssertError(result, WorkHourErrors.EndMustBeAfterStart);
        Assert.Null(workHour.WhEnd);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task End_EmployeeGone_IsNotFound()
    {
        // Arrange
        WithEmployee(null);

        // Act
        Result<WorkHourResponse> result = await End(FixRequest());

        // Assert
        AssertError(result, WorkHourErrors.EmployeeNotFound);
        VerifySaved(Times.Never());
    }

    // the plan's records are added and saved once; the lessons and the records of the whole period are loaded
    [Fact]
    public async Task AutoGenerate_AddsThePlannedRecords()
    {
        // Arrange
        WorkHourEmployee employee = Employee() with
        {
            WorkHoursStart = new DateTime(1899, 12, 30, 9, 0, 0, DateTimeKind.Unspecified)
        };
        _repository.Setup(r => r.GetEmployees(It.IsAny<CancellationToken>())).ReturnsAsync([employee]);
        _repository.Setup(r => r.GetLessonTimes(At(9, 1), At(11, 1), It.IsAny<CancellationToken>())).ReturnsAsync([
            new LessonTimeData(At(9, 15, 10), 1.5f), new LessonTimeData(At(9, 16, 10), 2f),
            new LessonTimeData(At(9, 17, 10), 1f)
        ]);
        _repository.Setup(r => r.GetRecordDays(At(9, 1), At(11, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkHourDay(15, At(9, 16))]);
        List<WorkHour> added = [];
        _repository.Setup(r => r.Add(It.IsAny<WorkHour>())).Callback<WorkHour>(added.Add);

        // Act
        Result<WorkHoursAutoGenerateResponse> result = await AutoGenerate(At(9, 1, 12), At(10, 31, 12));

        // Assert
        Assert.Equal(2, result.Value.CreatedCount);
        Assert.Equal([(15, At(9, 15, 9), At(9, 15, 11, 30)), (15, At(9, 17, 9), At(9, 17, 11))],
            added.ConvertAll(w => (w.TeacherContractId, w.WhStart, w.WhEnd ?? DateTime.MinValue)));
        VerifySaved(Times.Once());
    }

    // "today" is the local day: at 01:30 on 1 October in Georgia (still 30 September in UTC) 30.09 is a finished day
    // and is generated, 1.10 is not
    [Fact]
    public async Task AutoGenerate_GeneratesTheDaysBeforeTheLocalToday()
    {
        // Arrange
        _repository.Setup(r => r.GetEmployees(It.IsAny<CancellationToken>())).ReturnsAsync([Employee()]);
        _repository.Setup(r => r.GetLessonTimes(At(9, 1), At(10, 2), It.IsAny<CancellationToken>())).ReturnsAsync([
            new LessonTimeData(At(9, 30, 10), 1.5f), new LessonTimeData(At(10, 1, 0, 30), 1f)
        ]);
        _repository.Setup(r => r.GetRecordDays(At(9, 1), At(10, 2), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        List<WorkHour> added = [];
        _repository.Setup(r => r.Add(It.IsAny<WorkHour>())).Callback<WorkHour>(added.Add);

        // Act
        Result<WorkHoursAutoGenerateResponse> result = await AutoGenerate(At(9, 1), At(10, 1));

        // Assert
        Assert.Equal(1, result.Value.CreatedCount);
        WorkHour workHour = Assert.Single(added);
        Assert.Equal(At(9, 30, 10), workHour.WhStart);
        Assert.Equal(At(9, 30, 11, 30), workHour.WhEnd);
    }
}
