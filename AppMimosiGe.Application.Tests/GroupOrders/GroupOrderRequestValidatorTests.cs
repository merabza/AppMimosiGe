using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.CreateGroupOrder;
using AppMimosiGe.Application.GroupOrders.UpdateGroupOrder;
using AppMimosiGe.Application.GroupOrders.Validation;
using AppMimosiGe.Application.Groups;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using MimosiGeCore.Domain.Models;
using Moq;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrderRequestValidatorTests
{
    private static readonly DateTime OrderDate = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private readonly Mock<IGroupsRepository> _groupsRepository = RepositoryWhereEverythingExists();
    private readonly Mock<IGroupOrdersRepository> _repository = new();

    public GroupOrderRequestValidatorTests()
    {
        _repository.Setup(r => r.GroupExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    //ტიპის სწორი მოთხოვნა (GROUP_ORDERS.md §4): ჯგუფი 3, გადაყვანისას სამიზნე 4
    private static GroupOrderRequest Request(EGroupOrderType orderType)
    {
        bool createsGroup = orderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup;
        bool isInitial = orderType == EGroupOrderType.Initial;
        return new GroupOrderRequest
        {
            OrderType = (int)orderType,
            OrderDate = OrderDate,
            GroupId = createsGroup ? null : 3,
            TargetGroupId = orderType == EGroupOrderType.TransferStudents ? 4 : null,
            AcademicYearId = createsGroup ? 11 : null,
            GroupCode = createsGroup ? "601" : null,
            CourseId = createsGroup ? 6 : null,
            GroupSizeId = createsGroup ? 2 : null,
            StudentStatusId = createsGroup ? 10 : null,
            Teachers = orderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup
                or EGroupOrderType.ChangeTeacher or EGroupOrderType.ChangeSalaryScheme
                ? [Teacher(isInitial)]
                : [],
            Students = orderType switch
            {
                EGroupOrderType.ChangeTeacher or EGroupOrderType.ChangeSchedule or EGroupOrderType.CloseGroup
                    or EGroupOrderType.ChangeSalaryScheme => [],
                EGroupOrderType.RemoveStudents => [new GroupOrderStudentRequest { StudentContractId = 20 }],
                _ => [Student(20, isInitial)]
            },
            DayTimePlaces = orderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup
                or EGroupOrderType.ChangeSchedule or EGroupOrderType.ChangeWeeklyHours
                ? [DayTimePlace(isInitial)]
                : []
        };
    }

    private static GroupOrderTeacherRequest Teacher(bool withDates = false, int teacherContractId = 5,
        int? salarySchemaId = 8)
    {
        return new GroupOrderTeacherRequest
        {
            TeacherContractId = teacherContractId,
            SalarySchemaId = salarySchemaId,
            StartDate = withDates ? OrderDate : null
        };
    }

    private static GroupOrderStudentRequest Student(int studentContractId, bool withDates = false)
    {
        return new GroupOrderStudentRequest
        {
            StudentContractId = studentContractId,
            FourWeekHours = withDates ? 8 : null,
            FourWeekFee = 48,
            OneHourFee = 6,
            HoursCoefficient = 1,
            StartDate = withDates ? OrderDate : null
        };
    }

    private static GroupOrderDayTimePlaceRequest DayTimePlace(bool withDates = false)
    {
        return new GroupOrderDayTimePlaceRequest
        {
            WeekDayId = 2, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1, StartDate = withDates ? OrderDate : null
        };
    }

    private static GroupOrderRequest With(GroupOrderRequest request, Action<GroupOrderRequestBuilder> change)
    {
        var builder = new GroupOrderRequestBuilder(request);
        change(builder);
        return builder.Build();
    }

    private async Task<List<string>> ErrorCodes(GroupOrderRequest request)
    {
        var validator = new GroupOrderRequestValidator(_repository.Object, _groupsRepository.Object,
            StudentContractsRepositoryWhereEverythingExists().Object,
            TeacherContractsRepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(request);
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    [Theory]
    [InlineData(EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.CreateGroup)]
    [InlineData(EGroupOrderType.ChangeTeacher)]
    [InlineData(EGroupOrderType.AddStudents)]
    [InlineData(EGroupOrderType.RemoveStudents)]
    [InlineData(EGroupOrderType.TransferStudents)]
    [InlineData(EGroupOrderType.ChangeSchedule)]
    [InlineData(EGroupOrderType.CloseGroup)]
    [InlineData(EGroupOrderType.ChangeStudentFees)]
    [InlineData(EGroupOrderType.ChangeSalaryScheme)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours)]
    public async Task Validate_EveryTypeWithItsParts_IsValid(EGroupOrderType orderType)
    {
        Assert.Empty(await ErrorCodes(Request(orderType)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task Validate_UnknownType_IsTheOnlyError(int orderType)
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.OrderType = orderType);

        Assert.Equal([nameof(GroupOrderErrors.OrderTypeIsInvalid)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_NoOrderDate_IsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.OrderDate = default);

        Assert.Equal([nameof(GroupOrderErrors.OrderDateIsRequired)], await ErrorCodes(request));
    }

    //JSON-ის null სიებზე ვალიდატორი არ ვარდება
    [Fact]
    public async Task Validate_NullLists_AreReportedWithoutOtherRules()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.Students = null!);

        Assert.Equal(["NotNullValidator"], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_CreateOrderWithGroup_CannotHaveGroup()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.CreateGroup), b => b.GroupId = 3);

        Assert.Equal([nameof(GroupOrderErrors.CreateOrderCannotHaveGroup)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_InitialOrderWithGroup_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.Initial), b => b.GroupId = 3);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_StudentsOrderWithoutGroup_GroupIsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.GroupId = null);

        Assert.Equal([nameof(GroupOrderErrors.GroupIsRequired)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_UnknownGroupAndTarget_AreNotFound()
    {
        _repository.Setup(r => r.GroupExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.Equal([nameof(GroupOrderErrors.GroupNotFound), nameof(GroupOrderErrors.TargetGroupNotFound)],
            await ErrorCodes(Request(EGroupOrderType.TransferStudents)));
    }

    [Fact]
    public async Task Validate_TransferWithoutTarget_TargetIsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.TransferStudents), b => b.TargetGroupId = null);

        Assert.Equal([nameof(GroupOrderErrors.TargetGroupIsRequired)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_TransferToTheSameGroup_IsRefused()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.TransferStudents), b => b.TargetGroupId = 3);

        Assert.Equal([nameof(GroupOrderErrors.TargetGroupIsSourceGroup)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_TargetInAnotherType_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.TargetGroupId = 4);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_CreateOrderWithoutGroupFields_ReportsEach()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.CreateGroup), b =>
        {
            b.AcademicYearId = null;
            b.GroupCode = " ";
            b.CourseId = null;
            b.GroupSizeId = null;
            b.StudentStatusId = null;
        });

        Assert.Equal(
        [
            nameof(GroupErrors.AcademicYearNotFound), nameof(GroupErrors.GroupCodeIsRequired),
            nameof(GroupErrors.CourseNotFound), nameof(GroupErrors.GroupSizeNotFound),
            nameof(GroupErrors.StudentStatusNotFound)
        ], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_GroupCodeLongerThanTheColumn_IsTooLong()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.CreateGroup), b => b.GroupCode = " 123456 ");

        Assert.Equal([nameof(GroupErrors.GroupCodeIsTooLong)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_GroupFieldsOrVoidDateInAnotherType_PartIsNotAllowed()
    {
        GroupOrderRequest withCode = With(Request(EGroupOrderType.CloseGroup), b => b.GroupCode = "601");
        GroupOrderRequest withVoidDate = With(Request(EGroupOrderType.CreateGroup), b => b.VoidDate = OrderDate);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(withCode));
        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(withVoidDate));
    }

    [Fact]
    public async Task Validate_TeacherOrderWithoutTeacher_TeacherIsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.ChangeTeacher), b => b.Teachers = []);

        Assert.Equal([nameof(GroupOrderErrors.TeacherIsRequired)], await ErrorCodes(request));
    }

    [Theory]
    [InlineData(EGroupOrderType.ChangeSalaryScheme)]
    [InlineData(EGroupOrderType.CreateGroup)]
    public async Task Validate_TwoTeachers_OnlyOneIsAllowed(EGroupOrderType orderType)
    {
        GroupOrderRequest request = With(Request(orderType), b => b.Teachers = [Teacher(), Teacher()]);

        Assert.Equal([nameof(GroupOrderErrors.OnlyOneTeacherIsAllowed)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_TeacherInAStudentsOrder_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.Teachers = [Teacher()]);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    //D186: მოსწავლის გარეშე შექმნის ბრძანებაც არ ინახება
    [Theory]
    [InlineData(EGroupOrderType.CreateGroup)]
    [InlineData(EGroupOrderType.RemoveStudents)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours)]
    public async Task Validate_StudentsOrderWithoutStudents_StudentsAreRequired(EGroupOrderType orderType)
    {
        GroupOrderRequest request = With(Request(orderType), b => b.Students = []);

        Assert.Equal([nameof(GroupOrderErrors.StudentsAreRequired)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_StudentTwice_IsRepeated()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.Students = [Student(20), Student(20)]);

        Assert.Equal([nameof(GroupOrderErrors.StudentIsRepeated)], await ErrorCodes(request));
    }

    //საწყის ბრძანებაში ერთ მოსწავლეს ისტორიაში რამდენიმე სტრიქონი შეიძლება ჰქონდეს
    [Fact]
    public async Task Validate_InitialOrderStudentTwice_IsValid()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.Initial),
            b => b.Students = [Student(20, true), Student(20, true)]);

        Assert.Empty(await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_StudentsInATeacherOrder_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.ChangeTeacher), b => b.Students = [Student(20)]);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_ScheduleOrderWithoutSchedule_ScheduleIsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.ChangeSchedule), b => b.DayTimePlaces = []);

        Assert.Equal([nameof(GroupOrderErrors.DayTimePlacesAreRequired)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_ScheduleInAStudentsOrder_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.DayTimePlaces = [DayTimePlace()]);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    [Theory]
    [InlineData(EGroupOrderType.AddStudents)]
    [InlineData(EGroupOrderType.TransferStudents)]
    [InlineData(EGroupOrderType.ChangeStudentFees)]
    public async Task Validate_StudentWithoutFee_TariffIsRequired(EGroupOrderType orderType)
    {
        GroupOrderStudentRequest student = Student(20);
        GroupOrderRequest request = With(Request(orderType), b => b.Students =
        [
            new GroupOrderStudentRequest
            {
                StudentContractId = student.StudentContractId,
                FourWeekFee = student.FourWeekFee,
                HoursCoefficient = student.HoursCoefficient
            }
        ]);

        Assert.Equal([nameof(GroupOrderErrors.StudentTariffIsRequired)], await ErrorCodes(request));
    }

    //საწყისი ბრძანების ხაზს საათებიც სჭირდება: ეს ისტორიაა (D196)
    [Fact]
    public async Task Validate_InitialOrderStudentWithoutHours_TariffIsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.Initial), b => b.Students = [Student(20)]);

        Assert.Contains(nameof(GroupOrderErrors.StudentTariffIsRequired), await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_RemovalWithTariff_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.RemoveStudents), b => b.Students = [Student(20)]);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    //D187: საათებს სერვერი ფორმულით ითვლის
    [Fact]
    public async Task Validate_HoursOutsideTheInitialOrder_PartIsNotAllowed()
    {
        GroupOrderStudentRequest student = Student(20, true);
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.Students =
        [
            new GroupOrderStudentRequest
            {
                StudentContractId = 20,
                FourWeekHours = student.FourWeekHours,
                FourWeekFee = 48,
                OneHourFee = 6,
                HoursCoefficient = 1
            }
        ]);

        Assert.Equal([nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_InitialOrderLinesWithoutStartDate_StartDateIsRequired()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.Initial), b =>
        {
            b.Teachers = [Teacher()];
            b.DayTimePlaces = [DayTimePlace()];
        });

        Assert.Equal([nameof(GroupErrors.StartDateIsRequired), nameof(GroupErrors.StartDateIsRequired)],
            await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_LineDatesOutsideTheInitialOrder_PartIsNotAllowed()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.CreateGroup), b =>
        {
            b.Teachers = [Teacher(true)];
            b.DayTimePlaces = [DayTimePlace(true)];
        });

        Assert.Equal(
        [
            nameof(GroupOrderErrors.PartIsNotAllowedForOrderType),
            nameof(GroupOrderErrors.PartIsNotAllowedForOrderType)
        ], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_LineReferences_AreChecked()
    {
        _groupsRepository.Setup(r => r.TeacherContractExists(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _groupsRepository.Setup(r => r.StudentContractExists(20, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _groupsRepository.Setup(r => r.RoomExists(1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.Equal(
        [
            nameof(GroupErrors.TeacherContractNotFound), nameof(GroupErrors.StudentContractNotFound),
            nameof(GroupErrors.RoomNotFound)
        ], await ErrorCodes(Request(EGroupOrderType.CreateGroup)));
    }

    //D187: სქემის გარეშე მოსულ ხაზს კონტრაქტის ძირითადი სქემა სჭირდება
    [Fact]
    public async Task Validate_TeacherWithoutSchemeAndDefault_SchemeIsRequired()
    {
        _groupsRepository.Setup(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        GroupOrderRequest request =
            With(Request(EGroupOrderType.ChangeTeacher), b => b.Teachers = [Teacher(salarySchemaId: null)]);

        Assert.Equal([nameof(GroupErrors.SalarySchemeIsRequired)], await ErrorCodes(request));
    }

    [Fact]
    public async Task Validate_LineValues_MustBePositiveAndTheNoteShort()
    {
        GroupOrderRequest request = With(Request(EGroupOrderType.ChangeWeeklyHours), b =>
        {
            b.Students =
            [
                new GroupOrderStudentRequest
                {
                    StudentContractId = 20,
                    FourWeekFee = 0,
                    OneHourFee = 6,
                    HoursCoefficient = -1,
                    Note = new string('x', 256)
                }
            ];
            b.DayTimePlaces =
            [
                new GroupOrderDayTimePlaceRequest { WeekDayId = 2, LessonStartTimeId = 4, HoursCount = 0, RoomId = 1 }
            ];
        });

        Assert.Equal(
        [
            nameof(GroupErrors.FeeMustBePositive), nameof(GroupErrors.HoursCoefficientMustBePositive),
            nameof(GroupErrors.NoteIsTooLong), nameof(GroupErrors.HoursCountMustBePositive)
        ], await ErrorCodes(request));
    }

    //D181: 00:00-ზე დაწყებული განრიგის სტრიქონი შუაღამის თეორიულ თარიღებს იძლევა
    [Fact]
    public async Task Validate_ScheduleAtMidnight_IsRefused()
    {
        _groupsRepository.Setup(r => r.GetLessonStartTime(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimeOnly(0, 0));

        Assert.Equal([nameof(GroupErrors.LessonStartTimeIsMidnight)],
            await ErrorCodes(Request(EGroupOrderType.ChangeSchedule)));
    }

    [Fact]
    public async Task CommandValidators_NullRequest_CouldNotBeDecrypted()
    {
        var createValidator = new CreateGroupOrderCommandValidator(_repository.Object, _groupsRepository.Object,
            StudentContractsRepositoryWhereEverythingExists().Object,
            TeacherContractsRepositoryWhereEverythingExists().Object);
        var updateValidator = new UpdateGroupOrderCommandValidator(_repository.Object, _groupsRepository.Object,
            StudentContractsRepositoryWhereEverythingExists().Object,
            TeacherContractsRepositoryWhereEverythingExists().Object);

        ValidationResult created = await createValidator.ValidateAsync(new CreateGroupOrderCommand(null));
        ValidationResult updated = await updateValidator.ValidateAsync(new UpdateGroupOrderCommand(7, null));

        Assert.Equal(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code, Assert.Single(created.Errors).ErrorCode);
        Assert.Equal(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code, Assert.Single(updated.Errors).ErrorCode);
    }

    [Fact]
    public async Task CommandValidators_ValidateTheRequest()
    {
        var createValidator = new CreateGroupOrderCommandValidator(_repository.Object, _groupsRepository.Object,
            StudentContractsRepositoryWhereEverythingExists().Object,
            TeacherContractsRepositoryWhereEverythingExists().Object);
        var updateValidator = new UpdateGroupOrderCommandValidator(_repository.Object, _groupsRepository.Object,
            StudentContractsRepositoryWhereEverythingExists().Object,
            TeacherContractsRepositoryWhereEverythingExists().Object);
        GroupOrderRequest request = With(Request(EGroupOrderType.AddStudents), b => b.GroupId = null);

        ValidationResult created = await createValidator.ValidateAsync(new CreateGroupOrderCommand(request));
        ValidationResult updated = await updateValidator.ValidateAsync(new UpdateGroupOrderCommand(7, request));

        Assert.Equal(nameof(GroupOrderErrors.GroupIsRequired), Assert.Single(created.Errors).ErrorCode);
        Assert.Equal(nameof(GroupOrderErrors.GroupIsRequired), Assert.Single(updated.Errors).ErrorCode);
    }
}
