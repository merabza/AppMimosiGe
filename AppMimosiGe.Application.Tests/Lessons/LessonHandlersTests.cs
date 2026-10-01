using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Lessons.GetLesson;
using AppMimosiGe.Application.Lessons.GetLessonFormLookups;
using AppMimosiGe.Application.Lessons.GetLessonsRowsData;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGe.Application.Lessons.UpdateLesson;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class LessonHandlersTests
{
    private static readonly DateTime LessonDt = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Unspecified);

    private readonly List<StudentContract> _loadedContracts = [];
    private readonly Mock<ILessonsRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public LessonHandlersTests()
    {
        _repository.Setup(r => r.GetStudentContractsForChange(It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _loadedContracts.Clear();
                _loadedContracts.AddRange([Contract(10), Contract(11)]);
                return _loadedContracts;
            });
    }

    private static StudentContract Contract(int scId)
    {
        return new StudentContract { ScId = scId, ContractNumber = $"6.0{scId}", DirtyNextPayDate = false };
    }

    //a lesson as the generator left it, with one entered row and one empty row
    private static Lesson ExistingLesson()
    {
        var lesson = new Lesson
        {
            Id = 9,
            GroupId = 7,
            TeacherContractId = 3,
            LessonDt = LessonDt,
            SalarySchemaId = 4,
            FourWeekHours = 8f,
            TeoMinDate = LessonDt.AddDays(-2),
            TeoMaxDate = LessonDt.AddDays(2),
            Note = "old"
        };
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = 21,
            LessonId = 9,
            StudentContractId = 10,
            GroupByStudentId = 31,
            HoursCount = 1.5f,
            Present = true,
            Theme = "theme",
            Rate = 9,
            TeacherComment = "good",
            StudentComment = "ok",
            StudentLateMinutes = 5
        });
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = 22, LessonId = 9, StudentContractId = 11, GroupByStudentId = 32, HoursCount = 1.5f
        });
        return lesson;
    }

    private Lesson WithExistingLesson()
    {
        Lesson lesson = ExistingLesson();
        _repository.Setup(r => r.GetForChange(9, It.IsAny<CancellationToken>())).ReturnsAsync(lesson);
        return lesson;
    }

    private Task<Result> Update(LessonRequest request)
    {
        return new UpdateLessonCommandHandler(_repository.Object, _unitOfWork.Object).Handle(
            new UpdateLessonCommand(9, request), CancellationToken.None);
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task Update_AppliesTheEditableLessonFieldsAndLeavesTheGeneratorFields()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        DateTime recoverDate = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 3,
            SubstituteTeacherContractId = 5,
            TeacherLateMinutes = 12,
            RecoverDate = recoverDate.AddHours(10),
            Note = "  replaced  "
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, lesson.LessonStatusId);
        Assert.Equal(5, lesson.SubstituteTeacherContractId);
        Assert.Equal(12, lesson.TeacherLateMinutes);
        Assert.Equal(recoverDate, lesson.RecoverDate);
        Assert.Equal("replaced", lesson.Note);
        Assert.Equal(7, lesson.GroupId);
        Assert.Equal(3, lesson.TeacherContractId);
        Assert.Equal(LessonDt, lesson.LessonDt);
        Assert.Equal(4, lesson.SalarySchemaId);
        Assert.Equal(8f, lesson.FourWeekHours);
        Assert.Equal(LessonDt.AddDays(-2), lesson.TeoMinDate);
        Assert.Equal(LessonDt.AddDays(2), lesson.TeoMaxDate);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_EmptyValues_ClearTheLessonFields()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        lesson.SubstituteTeacherContractId = 5;
        lesson.RecoverDate = LessonDt;

        // Act
        await Update(new LessonRequest { LessonStatusId = 1, Note = "   " });

        // Assert
        Assert.Null(lesson.SubstituteTeacherContractId);
        Assert.Null(lesson.RecoverDate);
        Assert.Null(lesson.Note);
    }

    [Fact]
    public async Task Update_AppliesTheStudentFieldsAndLeavesHoursAndContract()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();

        // Act
        await Update(new LessonRequest
        {
            LessonStatusId = 1,
            Students =
            [
                new LessonStudentRequest
                {
                    Id = 22,
                    Present = true,
                    Theme = " new theme ",
                    Rate = 7.5f,
                    TeacherComment = "tc",
                    StudentComment = "sc",
                    StudentLateMinutes = 3
                }
            ]
        });

        // Assert
        LessonByStudent row = Assert.Single(lesson.LessonsByStudents, s => s.Id == 22);
        Assert.True(row.Present);
        Assert.Equal("new theme", row.Theme);
        Assert.Equal(7.5f, row.Rate);
        Assert.Equal("tc", row.TeacherComment);
        Assert.Equal("sc", row.StudentComment);
        Assert.Equal(3, row.StudentLateMinutes);
        Assert.Equal(11, row.StudentContractId);
        Assert.Equal(32, row.GroupByStudentId);
        Assert.Equal(1.5f, row.HoursCount);
    }

    // "clear" in the journal: the generator counts an empty string as entered data, so it is saved as NULL (Q17)
    [Fact]
    public async Task Update_ClearedRow_IsSavedWithoutEnteredData()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();

        // Act
        await Update(new LessonRequest
        {
            LessonStatusId = 1,
            Students = [new LessonStudentRequest { Id = 21, Theme = "", TeacherComment = " ", StudentComment = "" }]
        });

        // Assert
        LessonByStudent row = Assert.Single(lesson.LessonsByStudents, s => s.Id == 21);
        Assert.False(row.Present);
        Assert.Null(row.Theme);
        Assert.Null(row.Rate);
        Assert.Null(row.TeacherComment);
        Assert.Null(row.StudentComment);
        Assert.Equal(0, row.StudentLateMinutes);
    }

    // a row left out of the request keeps its values
    [Fact]
    public async Task Update_RowNotInTheRequest_IsUnchanged()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();

        // Act
        await Update(new LessonRequest { LessonStatusId = 1, Students = [new LessonStudentRequest { Id = 22 }] });

        // Assert
        LessonByStudent row = Assert.Single(lesson.LessonsByStudents, s => s.Id == 21);
        Assert.True(row.Present);
        Assert.Equal("theme", row.Theme);
        Assert.Equal(9, row.Rate);
        Assert.Equal(5, row.StudentLateMinutes);
    }

    // Access FrmLessons passed the lesson ID to SetStudentNextPayDateDirtyForGroup; the group is used here
    [Fact]
    public async Task Update_MarksTheGroupsAndTheLessonsContractsDirty()
    {
        // Arrange
        WithExistingLesson();

        // Act
        await Update(new LessonRequest { LessonStatusId = 2 });

        // Assert
        _repository.Verify(r => r.GetStudentContractsForChange(7, 9, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, _loadedContracts.Count);
        Assert.All(_loadedContracts, c => Assert.True(c.DirtyNextPayDate));
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_MissingLesson_IsNotFoundAndSavesNothing()
    {
        // Act
        Result result = await Update(new LessonRequest { LessonStatusId = 1 });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.LessonNotFound.Code, result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_RowOfAnotherLesson_IsAnErrorAndChangesNothing()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 2, Students = [new LessonStudentRequest { Id = 22, Present = true }, new() { Id = 99 }]
        });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.StudentRowNotFound.Code, result.Error.Code);
        Assert.Equal(1, lesson.LessonStatusId);
        Assert.False(Assert.Single(lesson.LessonsByStudents, s => s.Id == 22).Present);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Get_ReturnsTheLesson()
    {
        // Arrange
        var lesson = new LessonResponse(9, 7, "1001", "Math", 3, "Alpha Ann / T3.01", LessonDt, "Senior", 8f, LessonDt,
            LessonDt, 1, null, 0, null, null, 8, 10, []);
        _repository.Setup(r => r.GetOne(9, It.IsAny<CancellationToken>())).ReturnsAsync(lesson);

        // Act
        Result<LessonResponse> result =
            await new GetLessonQueryHandler(_repository.Object).Handle(new GetLessonQuery(9), CancellationToken.None);

        // Assert
        Assert.Same(lesson, result.Value);
    }

    [Fact]
    public async Task Get_MissingLesson_IsNotFound()
    {
        // Act
        Result<LessonResponse> result =
            await new GetLessonQueryHandler(_repository.Object).Handle(new GetLessonQuery(9), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.LessonNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetFormLookups_ReturnsGroupsTeachersAndStatuses()
    {
        // Arrange
        List<LookupItemResponse> groups = [new(7, "1001 / 2026-2027")];
        List<LookupItemResponse> teachers = [new(3, "Alpha Ann / T3.01")];
        List<LookupItemResponse> statuses = [new(1, "held")];
        _repository.Setup(r => r.GetGroups(It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        _repository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>())).ReturnsAsync(teachers);
        _repository.Setup(r => r.GetLessonStatuses(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);

        // Act
        Result<LessonFormLookupsResponse> result = await new GetLessonFormLookupsQueryHandler(_repository.Object)
            .Handle(new GetLessonFormLookupsQuery(), CancellationToken.None);

        // Assert
        Assert.Same(groups, result.Value.Groups);
        Assert.Same(teachers, result.Value.TeacherContracts);
        Assert.Same(statuses, result.Value.LessonStatuses);
    }

    [Fact]
    public async Task GetRowsData_PassesTheFilterAndTheLocalTime()
    {
        // Arrange
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone)
            .Returns(TimeZoneInfo.CreateCustomTimeZone("Tbilisi", TimeSpan.FromHours(4), "Tbilisi", "Tbilisi"));
        var rows = new LessonsRowsDataResponse(0, 0, []);
        LessonsListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<LessonsListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<LessonsListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(rows);

        // Act
        Result<LessonsRowsDataResponse> result = await new GetLessonsRowsDataQueryHandler(_repository.Object,
                timeProvider.Object)
            .Handle(new GetLessonsRowsDataQuery(Encode(
                    """{"offset":10,"rowsCount":5,"filterFields":[{"fieldName":"grpId","value":"7"},{"fieldName":"unfilled","value":"true"}]}""")),
                CancellationToken.None);

        // Assert
        Assert.Same(rows, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(5, passed.RowsCount);
        Assert.Equal(7, passed.GrpId);
        Assert.True(passed.Unfilled);
        Assert.Equal(new DateTime(2026, 10, 1, 13, 30, 0, DateTimeKind.Unspecified), passed.Now);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("bnVsbA==")]
    public async Task GetRowsData_UnreadableRequest_IsInvalid(string filterSortRequest)
    {
        // Act
        Result<LessonsRowsDataResponse> result =
            await new GetLessonsRowsDataQueryHandler(_repository.Object, TimeProvider.System).Handle(
                new GetLessonsRowsDataQuery(filterSortRequest), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetRowsData_InvalidFilter_IsInvalidAndNotLoaded()
    {
        // Act
        Result<LessonsRowsDataResponse> result =
            await new GetLessonsRowsDataQueryHandler(_repository.Object, TimeProvider.System).Handle(
                new GetLessonsRowsDataQuery(Encode(
                    """{"offset":0,"rowsCount":5,"filterFields":[{"fieldName":"grpId","value":"x"}]}""")),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
        _repository.Verify(r => r.GetRowsData(It.IsAny<LessonsListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
