using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Lessons.GetLesson;
using AppMimosiGe.Application.Lessons.GetLessonFormLookups;
using AppMimosiGe.Application.Lessons.GetLessonsRowsData;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGe.Application.Lessons.UpdateLesson;
using AppMimosiGe.Application.StudentContracts;
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
    private readonly Mock<ILessonGeneratorRepository> _lessonGenerator = new();

    private readonly List<StudentContract> _loadedContracts = [];
    private readonly Mock<ILessonsRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public LessonHandlersTests()
    {
        _repository.Setup(r => r.GetStudentContractsForChange(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            _loadedContracts.Clear();
            _loadedContracts.AddRange([Contract(10), Contract(11)]);
            return _loadedContracts;
        });
        _repository.Setup(r => r.GetStudentNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, id => $"Student {id}"));
        //the calendar ends in December: the generator's horizon is 31.12.2026. By default the group has the lesson
        //with both of its students, so the journal makes no lesson erroneous
        _lessonGenerator.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified));
        WithSavedGroup(SavedGroup());
        //by default no teacher has another lesson at the lesson's time
        _repository.Setup(r => r.GetTeacherLessons(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>())).ReturnsAsync([
            new LookupItemResponse(3, "Alpha Ann / T3"), new LookupItemResponse(5, "Beta Bob / T5")
        ]);
    }

    //the held lessons of the teacher contract in the searched days
    private void WithTeacherLessons(int teacherContractId, params TeacherLesson[] lessons)
    {
        _repository.Setup(r => r.GetTeacherLessons(teacherContractId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([.. lessons]);
    }

    private void VerifyTeacherLessonsNotLoaded()
    {
        _repository.Verify(
            r => r.GetTeacherLessons(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    //group 7 as the lesson generator loads it: teacher contract 3, students 31 (contract 10) and 32 (contract 11) and
    //the Wednesday 15:00 schedule since September, with lesson 9 as it is saved
    private static Group SavedGroup(DateTime? voidDate = null, DateTime? endDateOfContract11 = null)
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var group = new Group { GrpId = 7, GroupCode = "1001", VoidDate = voidDate };
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 1, TeacherContractId = 3, SalarySchemaId = 4, StartDate = start
        });
        group.GroupsByStudents.Add(new GroupByStudent { GbsId = 31, StudentContractId = 10, StartDate = start });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 32, StudentContractId = 11, StartDate = start, EndDate = endDateOfContract11
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 1,
            WeekDayId = 3,
            LessonStartTime = new LessonStartTime { LstTime = new TimeOnly(15, 0) },
            HoursCount = 1.5f,
            StartDate = start
        });
        group.Lessons.Add(ExistingLesson());
        return group;
    }

    private void WithSavedGroup(Group group)
    {
        _lessonGenerator.Setup(r => r.GetGroupForGeneration(7, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
    }

    private static TimeProvider TimeProviderAt(DateTimeOffset now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
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
            Id = 22,
            LessonId = 9,
            StudentContractId = 11,
            GroupByStudentId = 32,
            HoursCount = 1.5f
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
        return new UpdateLessonCommandHandler(_repository.Object, _lessonGenerator.Object, _unitOfWork.Object,
            TimeProviderAt(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero))).Handle(
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
        lesson.LessonStatusId = 3;
        DateTime recoverDate = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1,
            SubstituteTeacherContractId = 5,
            TeacherLateMinutes = 12,
            RecoverDate = recoverDate.AddHours(10),
            Note = "  replaced  "
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, lesson.LessonStatusId);
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
        await Update(new LessonRequest { LessonStatusId = 1 });

        // Assert
        _repository.Verify(r => r.GetStudentContractsForChange(7, 9, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, _loadedContracts.Count);
        Assert.All(_loadedContracts, c => Assert.True(c.DirtyNextPayDate));
        VerifySaved(Times.Once());
    }

    // r12: a cancelled lesson (status 2 or 3) has no recover date
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Update_NotHeldLessonWithRecoverDate_IsAnErrorAndChangesNothing(int statusId)
    {
        // Arrange
        Lesson lesson = WithExistingLesson();

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = statusId,
            RecoverDate = LessonDt.AddDays(3),
            Students = [new LessonStudentRequest { Id = 21 }]
        });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.NotHeldLessonCannotHaveRecoverDate.Code, result.Error.Code);
        Assert.Equal(1, lesson.LessonStatusId);
        Assert.Null(lesson.RecoverDate);
        Assert.True(Assert.Single(lesson.LessonsByStudents, s => s.Id == 21).Present);
        VerifySaved(Times.Never());
    }

    // r12: a cancelled lesson has no present student, in the request or in a row left out of it
    [Theory]
    [InlineData(2, false, true)]
    [InlineData(3, false, true)]
    [InlineData(2, true, false)]
    [InlineData(3, true, false)]
    public async Task Update_NotHeldLessonWithPresentStudent_IsAnErrorAndChangesNothing(int statusId,
        bool leaveOutThePresentRow, bool markTheOtherRowPresent)
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        List<LessonStudentRequest> students = [new() { Id = 22, Present = markTheOtherRowPresent }];
        if (!leaveOutThePresentRow)
        {
            students.Add(new LessonStudentRequest { Id = 21 });
        }

        // Act
        Result result = await Update(new LessonRequest { LessonStatusId = statusId, Students = students });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.NotHeldLessonCannotHavePresentStudents.Code, result.Error.Code);
        Assert.Equal(1, lesson.LessonStatusId);
        Assert.True(Assert.Single(lesson.LessonsByStudents, s => s.Id == 21).Present);
        Assert.False(Assert.Single(lesson.LessonsByStudents, s => s.Id == 22).Present);
        VerifySaved(Times.Never());
    }

    // cancelling a lesson clears its attendance in the same request
    [Fact]
    public async Task Update_NotHeldLessonWithoutAttendanceAndRecoverDate_IsSaved()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        lesson.RecoverDate = LessonDt;

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 2, Students = [new LessonStudentRequest { Id = 21, Theme = "theme" }]
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, lesson.LessonStatusId);
        Assert.Null(lesson.RecoverDate);
        Assert.All(lesson.LessonsByStudents, s => Assert.False(s.Present));
        VerifySaved(Times.Once());
    }

    //D177: contract 11 left the group on the lesson's day, so the generator would delete its row; with attendance it
    //would stay as an extra student (error 14 of report r13)
    [Fact]
    public async Task Update_AttendanceOfAStudentNoLongerInTheGroup_IsConflictNamingHimAndChangesNothing()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        WithSavedGroup(SavedGroup(endDateOfContract11: LessonDt.Date));

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1, Note = "new", Students = [new LessonStudentRequest { Id = 22, Present = true }]
        });

        // Assert
        Assert.Equal(nameof(LessonErrors.ExtraStudentsCannotHaveEnteredData), result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains(": Student 11. ", result.Error.Description, StringComparison.Ordinal);
        _repository.Verify(r => r.GetStudentNames(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(11)), It.IsAny<CancellationToken>()));
        Assert.Equal("old", lesson.Note);
        Assert.False(Assert.Single(lesson.LessonsByStudents, s => s.Id == 22).Present);
        Assert.Empty(_loadedContracts);
        VerifySaved(Times.Never());
    }

    //two extra students are named alphabetically, not in the order of their rows
    [Fact]
    public async Task Update_DataForTwoStudentsNoLongerInTheGroup_NamesBothAlphabetically()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        lesson.LessonsByStudents.Add(StudentRow23());
        Group group = SavedGroup(endDateOfContract11: LessonDt.Date);
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 33,
            StudentContractId = 12,
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
            EndDate = LessonDt.Date
        });
        group.Lessons.Single().LessonsByStudents.Add(StudentRow23());
        WithSavedGroup(group);
        _repository.Setup(r => r.GetStudentNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [11] = "Zeta Zoe", [12] = "Alpha Ann" });

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1,
            Students =
            [
                new LessonStudentRequest { Id = 22, Present = true },
                new LessonStudentRequest { Id = 23, Theme = "theme" }
            ]
        });

        // Assert
        Assert.Equal(nameof(LessonErrors.ExtraStudentsCannotHaveEnteredData), result.Error.Code);
        Assert.Contains(": Alpha Ann, Zeta Zoe. ", result.Error.Description, StringComparison.Ordinal);
        VerifySaved(Times.Never());
    }

    //the row of contract 12 (group row 33) without entered data
    private static LessonByStudent StudentRow23()
    {
        return new LessonByStudent
        {
            Id = 23,
            LessonId = 9,
            StudentContractId = 12,
            GroupByStudentId = 33,
            HoursCount = 1.5f
        };
    }

    //the lesson starts on the group's void date: it is extra and its attended row 21 keeps it already (error 11)
    [Fact]
    public async Task Update_AttendanceInALessonTheGroupNoLongerHas_IsConflictAndChangesNothing()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        WithSavedGroup(SavedGroup(LessonDt.Date));

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1, Students = [new LessonStudentRequest { Id = 22, Theme = "theme" }]
        });

        // Assert
        Assert.Equal(nameof(LessonErrors.ExtraLessonCannotHaveEnteredData), result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Null(Assert.Single(lesson.LessonsByStudents, s => s.Id == 22).Theme);
        _repository.Verify(r => r.GetStudentNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifySaved(Times.Never());
    }

    //the erroneous row 21 is in report r13 already: editing it or the other fields creates no new error
    [Fact]
    public async Task Update_LessonThatWasAlreadyErroneous_IsSavedWithoutNewEnteredData()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        WithSavedGroup(SavedGroup(LessonDt.Date));

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1,
            Note = "new",
            Students =
            [
                new LessonStudentRequest { Id = 21, Theme = "new theme" }, new LessonStudentRequest { Id = 22 }
            ]
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new", lesson.Note);
        Assert.Equal("new theme", Assert.Single(lesson.LessonsByStudents, s => s.Id == 21).Theme);
        VerifySaved(Times.Once());
    }

    //D178: substitute 5 teaches group 1002 on 30.09 14:00–15:30, and this lesson is 15:00–16:30
    [Fact]
    public async Task Update_NewSubstituteWithAnotherLessonAtThisTime_IsConflictAndChangesNothing()
    {
        // Arrange
        Lesson lesson = WithExistingLesson();
        WithTeacherLessons(5, new TeacherLesson(77, "1002", LessonDt.AddHours(-1), 1.5f));

        // Act
        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1, SubstituteTeacherContractId = 5, Note = "new"
        });

        // Assert
        Assert.Equal(nameof(LessonErrors.TeacherHasAnotherLessonAtThisTime), result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.StartsWith(
            "Beta Bob / T5 ამ გაკვეთილის დროს სხვა გაკვეთილს ატარებს: ჯგუფი 1002, 30.09.2026 14:00–15:30. ",
            result.Error.Description, StringComparison.Ordinal);
        Assert.Null(lesson.SubstituteTeacherContractId);
        Assert.Equal("old", lesson.Note);
        Assert.Empty(_loadedContracts);
        _repository.Verify(
            r => r.GetTeacherLessons(5, LessonDt.Date.AddDays(-1), LessonDt.Date.AddDays(1), 9,
                It.IsAny<CancellationToken>()), Times.Once);
        VerifySaved(Times.Never());
    }

    //the lesson lasts as long as its longest student row: 1.5 hours, so 16:30, though student 22 has 1 hour
    [Fact]
    public async Task Update_NewSubstitute_LessonLastsAsLongAsItsLongestStudentRow()
    {
        Lesson lesson = WithExistingLesson();
        Assert.Single(lesson.LessonsByStudents, s => s.Id == 22).HoursCount = 1f;
        WithTeacherLessons(5, new TeacherLesson(77, "1002", LessonDt.AddHours(1), 1f));

        Result result = await Update(new LessonRequest { LessonStatusId = 1, SubstituteTeacherContractId = 5 });

        Assert.Equal(nameof(LessonErrors.TeacherHasAnotherLessonAtThisTime), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //the substitute's other lesson ends when this one starts
    [Fact]
    public async Task Update_NewSubstituteFreeAtThisTime_IsSaved()
    {
        Lesson lesson = WithExistingLesson();
        WithTeacherLessons(5, new TeacherLesson(77, "1002", LessonDt.AddHours(-2), 2f));

        Result result = await Update(new LessonRequest { LessonStatusId = 1, SubstituteTeacherContractId = 5 });

        Assert.True(result.IsSuccess);
        Assert.Equal(5, lesson.SubstituteTeacherContractId);
        VerifySaved(Times.Once());
    }

    //the saved substitute already had the overlap (report it, don't block the attendance): nothing is checked again
    [Fact]
    public async Task Update_SavedSubstituteWithAnOverlap_DoesNotStopOtherChanges()
    {
        Lesson lesson = WithExistingLesson();
        lesson.SubstituteTeacherContractId = 5;
        WithTeacherLessons(5, new TeacherLesson(77, "1002", LessonDt, 1.5f));

        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 1, SubstituteTeacherContractId = 5, Note = "new"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("new", lesson.Note);
        VerifyTeacherLessonsNotLoaded();
        VerifySaved(Times.Once());
    }

    //without the substitute the lesson's own teacher 3 teaches it, who substitutes in group 1003 at that time
    [Fact]
    public async Task Update_RemovingTheSubstitute_ChecksTheLessonsTeacher()
    {
        Lesson lesson = WithExistingLesson();
        lesson.SubstituteTeacherContractId = 5;
        WithTeacherLessons(3, new TeacherLesson(78, "1003", LessonDt.AddMinutes(30), 1f));

        Result result = await Update(new LessonRequest { LessonStatusId = 1 });

        Assert.Equal(nameof(LessonErrors.TeacherHasAnotherLessonAtThisTime), result.Error.Code);
        Assert.StartsWith(
            "Alpha Ann / T3 ამ გაკვეთილის დროს სხვა გაკვეთილს ატარებს: ჯგუფი 1003, 30.09.2026 15:30–16:30. ",
            result.Error.Description, StringComparison.Ordinal);
        Assert.Equal(5, lesson.SubstituteTeacherContractId);
        VerifySaved(Times.Never());
    }

    //a cancelled lesson takes nobody's time
    [Fact]
    public async Task Update_CancelledLessonWithASubstitute_IsNotChecked()
    {
        Lesson lesson = WithExistingLesson();
        WithTeacherLessons(5, new TeacherLesson(77, "1002", LessonDt, 1.5f));

        Result result = await Update(new LessonRequest
        {
            LessonStatusId = 2,
            SubstituteTeacherContractId = 5,
            Students = [new LessonStudentRequest { Id = 21, Present = false }]
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(5, lesson.SubstituteTeacherContractId);
        VerifyTeacherLessonsNotLoaded();
    }

    //the saved cancelled lesson had substitute 5; held again, the substitute's time is taken
    [Fact]
    public async Task Update_CancelledLessonHeldAgain_IsChecked()
    {
        Lesson lesson = WithExistingLesson();
        lesson.LessonStatusId = 2;
        lesson.SubstituteTeacherContractId = 5;
        WithTeacherLessons(5, new TeacherLesson(77, "1002", LessonDt, 1.5f));

        Result result = await Update(new LessonRequest { LessonStatusId = 1, SubstituteTeacherContractId = 5 });

        Assert.Equal(nameof(LessonErrors.TeacherHasAnotherLessonAtThisTime), result.Error.Code);
        Assert.Equal(2, lesson.LessonStatusId);
        VerifySaved(Times.Never());
    }

    //recovered on 05.10, the lesson is held at 15:00 that day, when teacher 3 has group 1002
    [Fact]
    public async Task Update_RecoverDateOnAnotherLessonOfTheTeacher_IsConflict()
    {
        Lesson lesson = WithExistingLesson();
        var recoverDay = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);
        WithTeacherLessons(3, new TeacherLesson(79, "1002", recoverDay.AddHours(15.5), 1.5f));

        Result result = await Update(new LessonRequest { LessonStatusId = 1, RecoverDate = recoverDay.AddHours(10) });

        Assert.Equal(nameof(LessonErrors.TeacherHasAnotherLessonAtThisTime), result.Error.Code);
        Assert.Contains(": ჯგუფი 1002, 05.10.2026 15:30–17:00. ", result.Error.Description, StringComparison.Ordinal);
        Assert.Null(lesson.RecoverDate);
        _repository.Verify(
            r => r.GetTeacherLessons(3, recoverDay.AddDays(-1), recoverDay.AddDays(1), 9,
                It.IsAny<CancellationToken>()), Times.Once);
        VerifySaved(Times.Never());
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
            LessonStatusId = 2,
            Students = [new LessonStudentRequest { Id = 22, Present = true }, new LessonStudentRequest { Id = 99 }]
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
    public async Task GetFormLookups_ReturnsGroupsTeachersStatusesAndYears()
    {
        // Arrange
        List<LookupItemResponse> groups = [new(7, "1001 / 2026-2027")];
        List<LookupItemResponse> teachers = [new(3, "Alpha Ann / T3.01")];
        List<LookupItemResponse> statuses = [new(1, "held")];
        _repository.Setup(r => r.GetGroups(It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        _repository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>())).ReturnsAsync(teachers);
        _repository.Setup(r => r.GetLessonStatuses(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);
        var studentContractsRepository = new Mock<IStudentContractsRepository>();
        studentContractsRepository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 11,
                AcademicYearName = "2026-2027",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            },
            new AcademicYear
            {
                AyId = 10,
                AcademicYearName = "2025-2026",
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            }
        ]);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);

        // Act
        Result<LessonFormLookupsResponse> result =
            await new GetLessonFormLookupsQueryHandler(_repository.Object, studentContractsRepository.Object,
                timeProvider.Object).Handle(new GetLessonFormLookupsQuery(), CancellationToken.None);

        // Assert
        Assert.Same(groups, result.Value.Groups);
        Assert.Same(teachers, result.Value.TeacherContracts);
        Assert.Same(statuses, result.Value.LessonStatuses);
        Assert.Equal(11, result.Value.CurrentAcademicYearId);
        Assert.Equal([new LookupItemResponse(10, "2025-2026"), new LookupItemResponse(11, "2026-2027")],
            result.Value.AcademicYears);
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
        Result<LessonsRowsDataResponse> result =
            await new GetLessonsRowsDataQueryHandler(_repository.Object, timeProvider.Object).Handle(
                new GetLessonsRowsDataQuery(Encode(
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

    // not base64 (FormatException), not JSON (JsonException), JSON null (no request)
    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
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
