using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.CreateGroup;
using AppMimosiGe.Application.Groups.UpdateGroup;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupValidatorsTests
{
    private readonly Mock<IGroupsRepository> _repository = RepositoryWhereEverythingExists();

    private readonly Mock<IStudentContractsRepository> _studentContracts =
        StudentContractsRepositoryWhereEverythingExists();

    private readonly Mock<ITeacherContractsRepository> _teacherContracts =
        TeacherContractsRepositoryWhereEverythingExists();

    private async Task<string[]> ErrorCodes(GroupRequest request)
    {
        var validator = new GroupRequestValidator(_repository.Object, _studentContracts.Object,
            _teacherContracts.Object);
        ValidationResult result = await validator.ValidateAsync(request);
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    private CreateGroupCommandValidator CreateValidator()
    {
        return new CreateGroupCommandValidator(_repository.Object, _studentContracts.Object, _teacherContracts.Object);
    }

    private UpdateGroupCommandValidator UpdateValidator()
    {
        return new UpdateGroupCommandValidator(_repository.Object, _studentContracts.Object, _teacherContracts.Object);
    }

    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        DateTime nextMonth = StartDate.AddMonths(1);

        Assert.Empty(await ErrorCodes(ValidRequest("1001", StartDate.AddMonths(9),
            [Teacher(endDate: nextMonth), Teacher(teacherContractId: 6, startDate: nextMonth)],
            [Student(endDate: StartDate.AddDays(1), note: new string('ა', 255)), Student(studentContractId: 21)],
            [DayTimePlace(weekDayId: 1), DayTimePlace(weekDayId: 4)])));
    }

    // a new group may be saved before its teachers, students and schedule are known
    [Fact]
    public async Task EmptyRowLists_AreAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(teachers: [], students: [], dayTimePlaces: [])));
    }

    [Fact]
    public async Task NullRowLists_AreErrors()
    {
        var request = new GroupRequest
        {
            AcademicYearId = 11,
            GroupCode = "1001",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            Teachers = null!,
            Students = null!,
            DayTimePlaces = null!
        };

        Assert.Equal(3, (await ErrorCodes(request)).Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingGroupCode_IsRequiredError(string? groupCode)
    {
        Assert.Equal([GroupErrors.GroupCodeIsRequired.Code], await ErrorCodes(ValidRequest(groupCode)));
    }

    [Fact]
    public async Task GroupCodeLongerThanFive_IsTooLongError()
    {
        Assert.Equal([GroupErrors.GroupCodeIsTooLong.Code], await ErrorCodes(ValidRequest("100001")));
    }

    // the code is saved trimmed, so only its trimmed length counts
    [Fact]
    public async Task FiveCharacterCodeWithSpaces_IsAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(" 10001 ")));
    }

    [Fact]
    public async Task MissingReferences_AreNotFoundErrors()
    {
        _studentContracts.Reset();
        _teacherContracts.Reset();
        _repository.Reset();
        _repository.Setup(r => r.GetDefaultSalarySchemeId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultSalarySchemeId);

        Assert.Equal([
            GroupErrors.AcademicYearNotFound.Code, GroupErrors.CourseNotFound.Code, GroupErrors.GroupSizeNotFound.Code,
            GroupErrors.StudentStatusNotFound.Code, GroupErrors.TeacherContractNotFound.Code,
            GroupErrors.SalarySchemeNotFound.Code, GroupErrors.StudentContractNotFound.Code,
            GroupErrors.WeekDayNotFound.Code, GroupErrors.LessonStartTimeNotFound.Code, GroupErrors.RoomNotFound.Code
        ], await ErrorCodes(ValidRequest()));
    }

    [Fact]
    public async Task TeacherWithoutScheme_TakesTheContractsDefaultScheme()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(teachers: [Teacher(salarySchemaId: null)])));
        _teacherContracts.Verify(r => r.SalarySchemeExists(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TeacherWithoutSchemeWhoseContractHasNone_IsSchemeRequiredError()
    {
        _repository.Setup(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        Assert.Equal([GroupErrors.SalarySchemeIsRequired.Code],
            await ErrorCodes(ValidRequest(teachers: [Teacher(salarySchemaId: null)])));
    }

    // an explicit scheme does not need the contract's default one
    [Fact]
    public async Task TeacherWithSchemeWhoseContractHasNone_IsAllowed()
    {
        _repository.Setup(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        Assert.Empty(await ErrorCodes(ValidRequest(teachers: [Teacher(salarySchemaId: 8)])));
    }

    [Fact]
    public async Task MissingStartDates_AreRequiredErrors()
    {
        GroupRequest request = ValidRequest(
            teachers: [new GroupTeacherRequest { TeacherContractId = 5, SalarySchemaId = 8 }], students:
            [
                new GroupStudentRequest
                {
                    StudentContractId = 20, FourWeekHours = 8, FourWeekFee = 48, OneHourFee = 6, HoursCoefficient = 1
                }
            ], dayTimePlaces:
            [
                new GroupDayTimePlaceRequest { WeekDayId = 1, LessonStartTimeId = 17, HoursCount = 1, RoomId = 2 }
            ]);

        Assert.Equal(Enumerable.Repeat(GroupErrors.StartDateIsRequired.Code, 3), await ErrorCodes(request));
    }

    // [StartDate, EndDate) is empty when EndDate is not after StartDate
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task EndDateNotAfterStartDate_IsErrorInEveryRowType(int days)
    {
        DateTime endDate = StartDate.AddDays(days);

        Assert.Equal(Enumerable.Repeat(GroupErrors.EndDateMustBeAfterStartDate.Code, 3), await ErrorCodes(
            ValidRequest(teachers: [Teacher(endDate: endDate)], students: [Student(endDate: endDate)],
                dayTimePlaces: [DayTimePlace(endDate: endDate)])));
    }

    // only the days count: an end on the start day is an error even with a later time
    [Fact]
    public async Task EndDateOnStartDayWithLaterTime_IsError()
    {
        Assert.Equal([GroupErrors.EndDateMustBeAfterStartDate.Code],
            await ErrorCodes(ValidRequest(teachers: [Teacher(endDate: StartDate.AddHours(20))])));
    }

    [Fact]
    public async Task EndDateTheDayAfterStart_IsAllowed()
    {
        DateTime endDate = StartDate.AddDays(1);

        Assert.Empty(await ErrorCodes(ValidRequest(teachers: [Teacher(endDate: endDate)],
            students: [Student(endDate: endDate)], dayTimePlaces: [DayTimePlace(endDate: endDate)])));
    }

    // Access: valid=[>0] on the four tariff fields
    [Theory]
    [InlineData(0, 48, 6, 1, nameof(GroupErrors.FourWeekHoursMustBePositive))]
    [InlineData(-1, 48, 6, 1, nameof(GroupErrors.FourWeekHoursMustBePositive))]
    [InlineData(8, 0, 6, 1, nameof(GroupErrors.FeeMustBePositive))]
    [InlineData(8, 48, 0, 1, nameof(GroupErrors.FeeMustBePositive))]
    [InlineData(8, 48, -6, 1, nameof(GroupErrors.FeeMustBePositive))]
    [InlineData(8, 48, 6, 0, nameof(GroupErrors.HoursCoefficientMustBePositive))]
    public async Task NotPositiveTariff_IsError(float fourWeekHours, double fourWeekFee, double oneHourFee,
        float hoursCoefficient, string expectedErrorCode)
    {
        Assert.Equal([expectedErrorCode], await ErrorCodes(ValidRequest(students:
        [
            Student(fourWeekHours: fourWeekHours, fourWeekFee: (decimal)fourWeekFee, oneHourFee: (decimal)oneHourFee,
                hoursCoefficient: hoursCoefficient)
        ])));
    }

    [Fact]
    public async Task TooLongNote_IsError()
    {
        Assert.Equal([GroupErrors.NoteIsTooLong.Code],
            await ErrorCodes(ValidRequest(students: [Student(note: new string('ა', 256))])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public async Task NotPositiveLessonHours_IsError(float hoursCount)
    {
        Assert.Equal([GroupErrors.HoursCountMustBePositive.Code],
            await ErrorCodes(ValidRequest(dayTimePlaces: [DayTimePlace(hoursCount: hoursCount)])));
    }

    // generator error 5: two teachers on one day
    [Fact]
    public async Task OverlappingTeacherPeriods_AreError()
    {
        Assert.Equal([GroupErrors.TeacherPeriodsOverlap.Code], await ErrorCodes(ValidRequest(teachers:
        [
            Teacher(endDate: StartDate.AddMonths(2)),
            Teacher(teacherContractId: 6, startDate: StartDate.AddMonths(1))
        ])));
    }

    // generator error 7: two schedules on one week day
    [Fact]
    public async Task OverlappingSchedulesOfOneWeekDay_AreError()
    {
        Assert.Equal([GroupErrors.DayTimePlacePeriodsOverlap.Code], await ErrorCodes(ValidRequest(dayTimePlaces:
        [
            DayTimePlace(weekDayId: 2),
            DayTimePlace(weekDayId: 2, lessonStartTimeId: 20, startDate: StartDate.AddDays(7))
        ])));
    }

    [Fact]
    public async Task OverlappingSchedulesOfDifferentWeekDays_AreAllowed()
    {
        Assert.Empty(await ErrorCodes(ValidRequest(dayTimePlaces:
            [DayTimePlace(weekDayId: 2), DayTimePlace(weekDayId: 3)])));
    }

    [Fact]
    public async Task CreateValidator_NullRequest_IsDecryptError()
    {
        ValidationResult result = await CreateValidator().ValidateAsync(new CreateGroupCommand(null));

        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    // the trimmed code is checked, as it is saved trimmed
    [Fact]
    public async Task CreateValidator_TakenCode_IsConflict()
    {
        _repository.Setup(r => r.GroupCodeExists(11, "1001", 0, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ValidationResult result =
            await CreateValidator().ValidateAsync(new CreateGroupCommand(ValidRequest(" 1001 ")));

        Assert.Equal([GroupErrors.GroupCodeAlreadyExists.Code], result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task CreateValidator_FreeCode_IsValid()
    {
        ValidationResult result = await CreateValidator().ValidateAsync(new CreateGroupCommand(ValidRequest()));

        Assert.True(result.IsValid);
    }

    // the command validators apply the rules of the request
    [Fact]
    public async Task CommandValidators_InvalidRequest_AreReported()
    {
        GroupRequest request = ValidRequest(dayTimePlaces: [DayTimePlace(hoursCount: 0)]);

        ValidationResult created = await CreateValidator().ValidateAsync(new CreateGroupCommand(request));
        ValidationResult updated = await UpdateValidator().ValidateAsync(new UpdateGroupCommand(7, request));

        Assert.Equal([GroupErrors.HoursCountMustBePositive.Code], created.Errors.Select(e => e.ErrorCode));
        Assert.Equal([GroupErrors.HoursCountMustBePositive.Code], updated.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public async Task UpdateValidator_NullRequest_IsDecryptError()
    {
        ValidationResult result = await UpdateValidator().ValidateAsync(new UpdateGroupCommand(7, null));

        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    // the edited group itself is excluded from the uniqueness check
    [Fact]
    public async Task UpdateValidator_ChecksCodeExceptItself()
    {
        _repository.Setup(r => r.GroupCodeExists(11, "1001", 7, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ValidationResult taken = await UpdateValidator().ValidateAsync(new UpdateGroupCommand(7, ValidRequest()));
        ValidationResult free = await UpdateValidator().ValidateAsync(new UpdateGroupCommand(8, ValidRequest()));

        Assert.Equal([GroupErrors.GroupCodeAlreadyExists.Code], taken.Errors.Select(e => e.ErrorCode));
        Assert.True(free.IsValid);
    }

    // an empty code is already a required error, uniqueness is not asked
    [Fact]
    public async Task CommandValidators_EmptyCode_DoNotCheckUniqueness()
    {
        await CreateValidator().ValidateAsync(new CreateGroupCommand(ValidRequest(" ")));
        await UpdateValidator().ValidateAsync(new UpdateGroupCommand(7, ValidRequest(" ")));

        _repository.Verify(
            r => r.GroupCodeExists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}
