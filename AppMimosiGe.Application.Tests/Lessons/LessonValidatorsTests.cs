using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Lessons.UpdateLesson;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class LessonValidatorsTests
{
    private readonly Mock<ILessonsRepository> _repository = new();

    public LessonValidatorsTests()
    {
        _repository.Setup(r => r.LessonStatusExists(It.IsInRange(1, 3, Range.Inclusive), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.TeacherContractExists(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static LessonRequest Request(int lessonStatusId = 1, int? substituteTeacherContractId = null,
        int teacherLateMinutes = 0, string? note = null, params LessonStudentRequest[] students)
    {
        return new LessonRequest
        {
            LessonStatusId = lessonStatusId,
            SubstituteTeacherContractId = substituteTeacherContractId,
            TeacherLateMinutes = teacherLateMinutes,
            Note = note,
            Students = [.. students]
        };
    }

    private static LessonStudentRequest Student(int id = 1, int studentLateMinutes = 0, string? theme = null,
        string? teacherComment = null, string? studentComment = null)
    {
        return new LessonStudentRequest
        {
            Id = id,
            StudentLateMinutes = studentLateMinutes,
            Theme = theme,
            TeacherComment = teacherComment,
            StudentComment = studentComment
        };
    }

    private async Task<string[]> ErrorCodes(LessonRequest? request)
    {
        ValidationResult result = await new UpdateLessonCommandValidator(_repository.Object).ValidateAsync(
            new UpdateLessonCommand(9, request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        string longest = new('ა', 255);

        Assert.Empty(await ErrorCodes(Request(3, 5, 10, longest, Student(1, 15, longest, longest, longest),
            Student(2))));
    }

    // empty substitute, minutes 0 and no students are the defaults of a held lesson
    [Fact]
    public async Task MinimalRequest_HasNoErrors()
    {
        Assert.Empty(await ErrorCodes(Request()));
    }

    [Fact]
    public async Task NullRequest_CouldNotBeDecrypted()
    {
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code], await ErrorCodes(null));
    }

    [Fact]
    public async Task UnknownStatus_IsAnError()
    {
        Assert.Equal([LessonErrors.LessonStatusNotFound.Code], await ErrorCodes(Request(4)));
    }

    [Fact]
    public async Task UnknownSubstitute_IsAnError()
    {
        Assert.Equal([LessonErrors.SubstituteTeacherContractNotFound.Code],
            await ErrorCodes(Request(substituteTeacherContractId: 6)));
    }

    [Fact]
    public async Task NoSubstitute_IsNotLookedUp()
    {
        await ErrorCodes(Request());

        _repository.Verify(r => r.TeacherContractExists(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NegativeTeacherLateMinutes_IsAnError()
    {
        Assert.Equal([LessonErrors.TeacherLateMinutesMustNotBeNegative.Code],
            await ErrorCodes(Request(teacherLateMinutes: -1)));
    }

    [Fact]
    public async Task NegativeStudentLateMinutes_IsAnError()
    {
        Assert.Equal([LessonErrors.StudentLateMinutesMustNotBeNegative.Code],
            await ErrorCodes(Request(students: Student(studentLateMinutes: -1))));
    }

    [Fact]
    public async Task TextsLongerThan255_AreErrors()
    {
        string tooLong = new('ა', 256);

        Assert.Equal(
        [
            LessonErrors.NoteIsTooLong.Code, LessonErrors.ThemeIsTooLong.Code,
            LessonErrors.TeacherCommentIsTooLong.Code, LessonErrors.StudentCommentIsTooLong.Code
        ], await ErrorCodes(Request(note: tooLong, students: Student(theme: tooLong, teacherComment: tooLong,
            studentComment: tooLong))));
    }

    // the length is checked as it is saved: trimmed
    [Fact]
    public async Task SurroundingSpaces_DoNotCountToTheLength()
    {
        string padded = $"  {new string('ა', 255)}  ";

        Assert.Empty(await ErrorCodes(Request(note: padded, students: Student(theme: padded))));
    }

    [Fact]
    public async Task DuplicatedStudentRow_IsAnError()
    {
        Assert.Equal([LessonErrors.StudentRowIsDuplicated.Code],
            await ErrorCodes(Request(1, null, 0, null, Student(1), Student(2), Student(1))));
    }

    [Fact]
    public async Task NullStudents_IsAnError()
    {
        LessonRequest request = new() { LessonStatusId = 1, Students = null! };

        Assert.NotEmpty(await ErrorCodes(request));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    public void IsShortText_EmptyText_IsShort(string? value, bool expected)
    {
        Assert.Equal(expected, UpdateLessonCommandValidator.IsShortText(value));
    }
}
