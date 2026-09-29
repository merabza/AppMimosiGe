using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.CreateStudentContract;
using AppMimosiGe.Application.StudentContracts.DeleteStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;
using AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.StudentContracts.UpdateStudentContract;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.StudentContracts.StudentContractTestData;

namespace AppMimosiGe.Application.Tests.StudentContracts;

public sealed class StudentContractHandlersTests
{
    private readonly Mock<IStudentContractsRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static StudentContract ExistingContract()
    {
        var studentContract = new StudentContract
        {
            ScId = 42,
            ContractNumber = "6.001",
            ContractDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
            StudentHumanId = 1,
            PayerHumanId = 2,
            AcademicYearId = 11,
            NextPayDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
            DirtyNextPayDate = false
        };
        studentContract.StudentContractDetails.Add(new StudentContractDetail
        {
            Id = 100, StudentContractId = 42, CourseId = 1, GroupSizeId = 1
        });
        studentContract.StudentContractDetails.Add(new StudentContractDetail
        {
            Id = 101, StudentContractId = 42, CourseId = 2, GroupSizeId = 1
        });
        return studentContract;
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task Create_AddsContractWithDetailsAndSaves()
    {
        StudentContract? added = null;
        _repository.Setup(r => r.Add(It.IsAny<StudentContract>()))
            .Callback<StudentContract>(sc =>
            {
                added = sc;
                sc.ScId = 128;
            });
        var handler = new CreateStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);
        StudentContractRequest request = ValidRequest(details:
        [
            Detail(courseId: 5, fourWeekHours: 12, fourWeekFee: 72, oneHourFee: 6), Detail(courseId: 6)
        ]);

        Result<int> result = await handler.Handle(new CreateStudentContractCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(128, result.Value);
        Assert.NotNull(added);
        Assert.Equal("6.001", added.ContractNumber);
        Assert.Equal(request.ContractDate, added.ContractDate);
        Assert.Equal(1, added.StudentHumanId);
        Assert.Equal(2, added.PayerHumanId);
        Assert.Equal(11, added.AcademicYearId);
        Assert.Equal(3, added.StudentStatusId);
        Assert.Equal(10, added.DesiredMonthlyPaymentDay);
        Assert.True(added.DirtyNextPayDate);
        Assert.Equal([5, 6], added.StudentContractDetails.Select(d => d.CourseId));
        StudentContractDetail first = added.StudentContractDetails.First();
        Assert.Equal(12f, first.FourWeekHours);
        Assert.Equal(72m, first.FourWeekFee);
        Assert.Equal(6m, first.OneHourFee);
        Assert.Equal(2, first.GroupSizeId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFoundAndDoesNotSave()
    {
        var handler = new UpdateStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new UpdateStudentContractCommand(42, ValidRequest()),
            CancellationToken.None);

        Assert.Equal(StudentContractErrors.StudentContractNotFound.Code, result.Error.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_DetailOfAnotherContract_FailsWithoutChanges()
    {
        StudentContract existing = ExistingContract();
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new UpdateStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(
            new UpdateStudentContractCommand(42, ValidRequest("6.555", details: [Detail(999)])),
            CancellationToken.None);

        Assert.Equal(StudentContractErrors.DetailNotFound.Code, result.Error.Code);
        Assert.Equal("6.001", existing.ContractNumber);
        Assert.Equal([100, 101], existing.StudentContractDetails.Select(d => d.Id));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ChangesFieldsAndSynchronizesDetails()
    {
        StudentContract existing = ExistingContract();
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new UpdateStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);
        StudentContractRequest request = ValidRequest("6.002", studentHumanId: 5, payerHumanId: 5,
            studentStatusId: null, desiredMonthlyPaymentDay: null, details:
            [
                Detail(101, courseId: 7, fourWeekHours: 4, fourWeekFee: 30, oneHourFee: 7.5m), Detail(courseId: 8)
            ]);

        Result result = await handler.Handle(new UpdateStudentContractCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("6.002", existing.ContractNumber);
        Assert.Equal(5, existing.StudentHumanId);
        Assert.Equal(5, existing.PayerHumanId);
        Assert.Null(existing.StudentStatusId);
        Assert.Null(existing.DesiredMonthlyPaymentDay);
        //next pay date belongs to part 12 and is not touched here
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified), existing.NextPayDate);
        Assert.False(existing.DirtyNextPayDate);
        //detail 100 removed, 101 updated, one new detail added
        Assert.Equal([101, 0], existing.StudentContractDetails.Select(d => d.Id));
        StudentContractDetail updated = existing.StudentContractDetails.First();
        Assert.Equal(7, updated.CourseId);
        Assert.Equal(4f, updated.FourWeekHours);
        Assert.Equal(30m, updated.FourWeekFee);
        Assert.Equal(7.5m, updated.OneHourFee);
        Assert.Equal(8, existing.StudentContractDetails.Last().CourseId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        var handler = new DeleteStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteStudentContractCommand(42), CancellationToken.None);

        Assert.Equal(StudentContractErrors.StudentContractNotFound.Code, result.Error.Code);
        _repository.Verify(r => r.Remove(It.IsAny<StudentContract>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ContractInUse_IsForbidden()
    {
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingContract());
        _repository.Setup(r => r.IsInUse(42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new DeleteStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteStudentContractCommand(42), CancellationToken.None);

        Assert.Equal(StudentContractErrors.StudentContractIsInUse.Code, result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _repository.Verify(r => r.Remove(It.IsAny<StudentContract>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_UnusedContract_RemovesAndSaves()
    {
        StudentContract existing = ExistingContract();
        _repository.Setup(r => r.GetForChange(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new DeleteStudentContractCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteStudentContractCommand(42), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(existing), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOne_NotFound_ReturnsNotFound()
    {
        var handler = new GetStudentContractQueryHandler(_repository.Object);

        Result<StudentContractResponse> result =
            await handler.Handle(new GetStudentContractQuery(7), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetOne_Found_ReturnsContract()
    {
        var response = new StudentContractResponse(7, "6.007", DateTime.Today, 1, "S", 2, "P", 11, null, null, null,
            true, []);
        _repository.Setup(r => r.GetOne(7, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var handler = new GetStudentContractQueryHandler(_repository.Object);

        Result<StudentContractResponse> result =
            await handler.Handle(new GetStudentContractQuery(7), CancellationToken.None);

        Assert.Same(response, result.Value);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
    public async Task RowsData_UndecodableRequest_Fails(string filterSortRequest)
    {
        var handler = new GetStudentContractsRowsDataQueryHandler(_repository.Object);

        Result<StudentContractsRowsDataResponse> result =
            await handler.Handle(new GetStudentContractsRowsDataQuery(filterSortRequest), CancellationToken.None);

        Assert.Equal(StudentContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task RowsData_NullJson_Fails()
    {
        var handler = new GetStudentContractsRowsDataQueryHandler(_repository.Object);

        Result<StudentContractsRowsDataResponse> result =
            await handler.Handle(new GetStudentContractsRowsDataQuery(Encode("null")), CancellationToken.None);

        Assert.Equal(StudentContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task RowsData_InvalidFilter_FailsWithoutQueryingRepository()
    {
        var handler = new GetStudentContractsRowsDataQueryHandler(_repository.Object);
        string json = """{"offset":0,"rowsCount":10,"filterFields":[{"fieldName":"x","value":"1"}]}""";

        Result<StudentContractsRowsDataResponse> result =
            await handler.Handle(new GetStudentContractsRowsDataQuery(Encode(json)), CancellationToken.None);

        Assert.True(result.IsFailure);
        _repository.Verify(r => r.GetRowsData(It.IsAny<StudentContractsListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RowsData_ValidRequest_PassesTypedQueryToRepository()
    {
        var expected = new StudentContractsRowsDataResponse(0, 0, []);
        StudentContractsListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<StudentContractsListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<StudentContractsListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(expected);
        var handler = new GetStudentContractsRowsDataQueryHandler(_repository.Object);
        //the front end URL-encodes the JSON before base64 (Georgian text), the factory decodes it
        string json = Uri.EscapeDataString(
            """{"offset":10,"rowsCount":10,"filterFields":[{"fieldName":"search","value":"ბერიძე"}],"sortByFields":[{"fieldName":"contractDate","ascending":false}]}""");

        Result<StudentContractsRowsDataResponse> result =
            await handler.Handle(new GetStudentContractsRowsDataQuery(Encode(json)), CancellationToken.None);

        Assert.Same(expected, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal("ბერიძე", passed.Search);
        Assert.Equal([new StudentContractSortField(EStudentContractSortField.ContractDate, false)],
            passed.SortFields);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" a ")]
    public async Task SearchHumans_TooShort_ReturnsEmptyWithoutQuery(string? search)
    {
        var handler = new SearchHumansQueryHandler(_repository.Object);

        Result<List<LookupItemResponse>> result =
            await handler.Handle(new SearchHumansQuery(search), CancellationToken.None);

        Assert.Empty(result.Value);
        _repository.Verify(r => r.SearchHumans(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchHumans_TrimsAndLimits()
    {
        List<LookupItemResponse> expected = [new(1, "A B")];
        _repository.Setup(r => r.SearchHumans("ab", SearchHumansQueryHandler.MaxCount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new SearchHumansQueryHandler(_repository.Object);

        Result<List<LookupItemResponse>> result =
            await handler.Handle(new SearchHumansQuery("  ab "), CancellationToken.None);

        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task FormLookups_ComputesCurrentYearAndSortsYearsByName()
    {
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            },
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            }
        ]);
        List<LookupItemResponse> statuses = [new(1, "s")];
        List<LookupItemResponse> courses = [new(2, "c")];
        List<LookupItemResponse> groupSizes = [new(3, "g")];
        _repository.Setup(r => r.GetStudentStatuses(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);
        _repository.Setup(r => r.GetCourses(It.IsAny<CancellationToken>())).ReturnsAsync(courses);
        _repository.Setup(r => r.GetGroupSizes(It.IsAny<CancellationToken>())).ReturnsAsync(groupSizes);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        var handler = new GetStudentContractFormLookupsQueryHandler(_repository.Object, timeProvider.Object);

        Result<StudentContractFormLookupsResponse> result =
            await handler.Handle(new GetStudentContractFormLookupsQuery(), CancellationToken.None);

        Assert.Equal(11, result.Value.CurrentAcademicYearId);
        Assert.Equal([10, 11], result.Value.AcademicYears.Select(y => y.Id));
        Assert.Equal("2025-2026", result.Value.AcademicYears[0].Name);
        Assert.Same(statuses, result.Value.StudentStatuses);
        Assert.Same(courses, result.Value.Courses);
        Assert.Same(groupSizes, result.Value.GroupSizes);
    }
}
