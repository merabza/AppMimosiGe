using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.CreateGroup;
using AppMimosiGe.Application.Groups.DeleteGroup;
using AppMimosiGe.Application.Groups.GetGroup;
using AppMimosiGe.Application.Groups.GetGroupFormLookups;
using AppMimosiGe.Application.Groups.GetGroupsRowsData;
using AppMimosiGe.Application.Groups.GetGroupStudentContracts;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.Groups.UpdateGroup;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupHandlersTests
{
    private static readonly DateTime OtherDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);

    //Mondays of the group, which starts on Tuesday 15.09.2026
    private static readonly DateTime Monday21 = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<ILessonGeneratorRepository> _lessonGenerator = new();

    //the student contracts the handler loaded to set their DirtyNextPayDate flag
    private readonly List<StudentContract> _loadedContracts = [];
    private readonly Mock<IGroupsRepository> _repository = RepositoryWhereEverythingExists();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public GroupHandlersTests()
    {
        //the calendar ends in December: the generator's horizon is 31.12.2026. By default the saved group has no
        //lessons, so no change makes a lesson erroneous
        _lessonGenerator.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified));
        WithSavedGroup(SavedGroup());
        _repository.Setup(r => r.GetLessonStartTimesById(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, TimeOnly> { [17] = new(15, 0) });
        //by default the teachers teach in no other group
        _repository.Setup(r => r.GetTeacherSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _repository.Setup(r =>
                r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            {
                List<StudentContract> contracts =
                [
                    .. ids.Select(id => new StudentContract
                    {
                        ScId = id, ContractNumber = "6.001", DirtyNextPayDate = false
                    })
                ];
                _loadedContracts.AddRange(contracts);
                return contracts;
            });
    }

    //group 42 of year 11 with teacher 100, students 200 (contract 20) and 201 (contract 21), schedule 300
    private static Group ExistingGroup()
    {
        var group = new Group
        {
            GrpId = 42,
            AcademicYearId = 11,
            GroupCode = "1001",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            DirtyLessons = false
        };
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 100,
            GroupId = 42,
            TeacherContractId = 5,
            SalarySchemaId = 8,
            StartDate = StartDate
        });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 200, GroupId = 42, StudentContractId = 20, StartDate = StartDate
        });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 201, GroupId = 42, StudentContractId = 21, StartDate = StartDate
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 300,
            GroupId = 42,
            WeekDayId = 1,
            LessonStartTimeId = 17,
            RoomId = 2,
            StartDate = StartDate
        });
        return group;
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    private static TimeProvider TimeProviderAt(DateTimeOffset now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    private void AssertLoadedContractsAreDirty(params int[] expectedScIds)
    {
        Assert.Equal(expectedScIds.Order(), _loadedContracts.Select(c => c.ScId).Order());
        Assert.All(_loadedContracts, c => Assert.True(c.DirtyNextPayDate));
    }

    private void WithExistingGroup(Group group)
    {
        _repository.Setup(r => r.GetForChange(group.GrpId, It.IsAny<CancellationToken>())).ReturnsAsync(group);
    }

    //group 42 as the lesson generator loads it for the check of erroneous lessons: the schedule with its start time
    //(Monday 15:00), the lessons with their rows
    private static Group SavedGroup(DateTime? voidDate = null, params Lesson[] lessons)
    {
        Group group = ExistingGroup();
        group.VoidDate = voidDate;
        foreach (GroupDayTimePlace dayTimePlace in group.GroupDayTimePlaces)
        {
            dayTimePlace.LessonStartTime = new LessonStartTime { LstId = 17, LstTime = new TimeOnly(15, 0) };
        }

        foreach (Lesson lesson in lessons)
        {
            group.Lessons.Add(lesson);
        }

        return group;
    }

    //a lesson of group 42 where student 200 (contract 20) was present
    private static Lesson AttendedLesson(int id, DateTime lessonDt)
    {
        var lesson = new Lesson
        {
            Id = id,
            GroupId = 42,
            LessonDt = lessonDt,
            TeacherContractId = 5,
            SalarySchemaId = 8,
            FourWeekHours = 8f,
            TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt
        };
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = id * 10,
            LessonId = id,
            StudentContractId = 20,
            GroupByStudentId = 200,
            HoursCount = 1.5f,
            Present = true
        });
        return lesson;
    }

    private void WithSavedGroup(Group group)
    {
        _lessonGenerator.Setup(r => r.GetGroupForGeneration(group.GrpId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
    }

    //group 7 ("1002"), where teacher contract 5 teaches on Mondays 15:30–17:00 since 01.09, and the names of the error
    private void WithOtherGroupOfTeacher5()
    {
        var september1 = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
        _repository.Setup(r => r.GetTeacherSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([
            new TeacherScheduleGroup(7, "1002", null, [new TeacherScheduleTeacher(5, september1, null)],
                [new TeacherScheduleDayTime(1, new TimeOnly(15, 30), 1.5f, september1, null)])
        ]);
        _repository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroupTeacherContractLookupResponse(5, "Alpha Ann / T5", 8)]);
        _repository.Setup(r => r.GetWeekDays(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(1, "ორშაბათი")]);
    }

    private void VerifyTeacherSchedulesOfContract5Except(int grpId)
    {
        _repository.Verify(
            r => r.GetTeacherSchedules(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(5)), grpId,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    private UpdateGroupCommandHandler UpdateHandler()
    {
        return new UpdateGroupCommandHandler(_repository.Object, _lessonGenerator.Object, _unitOfWork.Object,
            TimeProviderAt(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Create_AddsGroupWithRowsMarksItDirtyAndSaves()
    {
        Group? added = null;
        _repository.Setup(r => r.Add(It.IsAny<Group>())).Callback<Group>(g =>
        {
            added = g;
            g.GrpId = 52;
        });
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
        GroupRequest request = ValidRequest(" 1011 ", OtherDate.AddHours(10), [Teacher(endDate: OtherDate.AddHours(3))],
            [Student(studentContractId: 20, note: "  შენიშვნა "), Student(studentContractId: 21, note: " ")],
            [DayTimePlace(weekDayId: 3, hoursCount: 2), DayTimePlace(weekDayId: 5)]);

        Result<int> result = await handler.Handle(new CreateGroupCommand(request), CancellationToken.None);

        Assert.Equal(52, result.Value);
        Assert.NotNull(added);
        Assert.Equal("1011", added.GroupCode);
        Assert.Equal(11, added.AcademicYearId);
        Assert.Equal(6, added.CourseId);
        Assert.Equal(2, added.GroupSizeId);
        Assert.Equal(10, added.StudentStatusId);
        Assert.Equal(OtherDate, added.VoidDate);
        Assert.True(added.DirtyLessons);
        GroupByTeacher teacher = Assert.Single(added.GroupsByTeachers);
        Assert.Equal(5, teacher.TeacherContractId);
        Assert.Equal(8, teacher.SalarySchemaId);
        Assert.Equal(StartDate, teacher.StartDate);
        Assert.Equal(OtherDate, teacher.EndDate);
        Assert.Equal([20, 21], added.GroupsByStudents.Select(s => s.StudentContractId));
        GroupByStudent student = added.GroupsByStudents.First();
        Assert.Equal(12f, student.FourWeekHours);
        Assert.Equal(72m, student.FourWeekFee);
        Assert.Equal(6m, student.OneHourFee);
        Assert.Equal(1f, student.HoursCoefficient);
        Assert.Equal("შენიშვნა", student.Note);
        Assert.Null(added.GroupsByStudents.Last().Note);
        Assert.Equal([3, 5], added.GroupDayTimePlaces.Select(d => d.WeekDayId));
        GroupDayTimePlace dayTimePlace = added.GroupDayTimePlaces.First();
        Assert.Equal(17, dayTimePlace.LessonStartTimeId);
        Assert.Equal(2f, dayTimePlace.HoursCount);
        Assert.Equal(2, dayTimePlace.RoomId);
        AssertLoadedContractsAreDirty(20, 21);
        VerifySaved(Times.Once());
    }

    // Access: choosing the teacher sets the scheme to the teacher contract's SalarySchemaByHours
    [Fact]
    public async Task Create_TeacherWithoutScheme_GetsTheContractsDefaultScheme()
    {
        Group? added = null;
        _repository.Setup(r => r.Add(It.IsAny<Group>())).Callback<Group>(g => added = g);
        _repository.Setup(r => r.GetDefaultSalarySchemeId(6, It.IsAny<CancellationToken>())).ReturnsAsync(14);
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
        GroupRequest request = ValidRequest(teachers:
        [
            Teacher(teacherContractId: 5, salarySchemaId: null, endDate: OtherDate),
            Teacher(teacherContractId: 6, salarySchemaId: null, startDate: OtherDate, endDate: OtherDate.AddMonths(1)),
            Teacher(teacherContractId: 7, salarySchemaId: 3, startDate: OtherDate.AddMonths(1))
        ]);

        await handler.Handle(new CreateGroupCommand(request), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal([DefaultSalarySchemeId, 14, 3], added.GroupsByTeachers.Select(t => t.SalarySchemaId));
        _repository.Verify(r => r.GetDefaultSalarySchemeId(7, It.IsAny<CancellationToken>()), Times.Never);
    }

    // one query per contract, even when it teaches in several rows
    [Fact]
    public async Task Create_SameContractInSeveralRowsWithoutScheme_IsLookedUpOnce()
    {
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
        GroupRequest request = ValidRequest(teachers:
        [
            Teacher(salarySchemaId: null, endDate: OtherDate), Teacher(salarySchemaId: null, startDate: OtherDate)
        ]);

        await handler.Handle(new CreateGroupCommand(request), CancellationToken.None);

        _repository.Verify(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_RowWithId_FailsWithoutSaving()
    {
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        Result<int> result = await handler.Handle(new CreateGroupCommand(ValidRequest(students: [Student(7)])),
            CancellationToken.None);

        Assert.Equal(GroupErrors.RowNotFound.Code, result.Error.Code);
        _repository.Verify(r => r.Add(It.IsAny<Group>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Create_WithoutStudents_LoadsNoContracts()
    {
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        Result<int> result = await handler.Handle(new CreateGroupCommand(ValidRequest(students: [])),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(
            r => r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifySaved(Times.Once());
    }

    //D178: the new group's Monday 15:00–16:30 since 15.09 overlaps group 7 of the same teacher
    [Fact]
    public async Task Create_TeacherBusyInAnotherGroup_IsConflictAndNothingIsAdded()
    {
        // Arrange
        WithOtherGroupOfTeacher5();
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        // Act
        Result<int> result = await handler.Handle(new CreateGroupCommand(ValidRequest()), CancellationToken.None);

        // Assert
        Assert.Equal(nameof(GroupErrors.TeacherTimesOverlap), result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains(": Alpha Ann / T5 — ჯგუფი 1002, ორშაბათი 15:30–17:00, 15.09.2026-დან. ",
            result.Error.Description, StringComparison.Ordinal);
        VerifyTeacherSchedulesOfContract5Except(0);
        _repository.Verify(r => r.Add(It.IsAny<Group>()), Times.Never);
        Assert.Empty(_loadedContracts);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Create_TeacherFreeAtThatTime_IsSaved()
    {
        WithOtherGroupOfTeacher5();
        var handler = new CreateGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        Result<int> result = await handler.Handle(
            new CreateGroupCommand(ValidRequest(dayTimePlaces: [DayTimePlace(weekDayId: 3)])), CancellationToken.None);

        Assert.True(result.IsSuccess);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFoundAndDoesNotSave()
    {
        UpdateGroupCommandHandler handler = UpdateHandler();

        Result result = await handler.Handle(new UpdateGroupCommand(42, ValidRequest()), CancellationToken.None);

        Assert.Equal(GroupErrors.GroupNotFound.Code, result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(999, 0, 0)]
    [InlineData(0, 999, 0)]
    [InlineData(0, 0, 999)]
    public async Task Update_RowOfAnotherGroup_FailsWithoutChanges(int teacherId, int studentId, int dayTimePlaceId)
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        UpdateGroupCommandHandler handler = UpdateHandler();
        GroupRequest request = ValidRequest("1099", teachers: [Teacher(teacherId)], students: [Student(studentId)],
            dayTimePlaces: [DayTimePlace(dayTimePlaceId)]);

        Result result = await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.Equal(GroupErrors.RowNotFound.Code, result.Error.Code);
        Assert.Equal("1001", group.GroupCode);
        Assert.False(group.DirtyLessons);
        _repository.Verify(r => r.RemoveRows(It.IsAny<GroupRemovedRows>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_SyncsRowsMarksDirtyAndSaves()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        GroupRemovedRows? removed = null;
        _repository.Setup(r => r.RemoveRows(It.IsAny<GroupRemovedRows>()))
            .Callback<GroupRemovedRows>(rows => removed = rows);
        UpdateGroupCommandHandler handler = UpdateHandler();
        //teacher 100 gets an end, a second teacher follows; student 200 moves to contract 22, student 201 leaves,
        //contract 23 joins; the schedule row 300 is replaced by a new one
        GroupRequest request = ValidRequest("1002", teachers:
        [
            Teacher(100, endDate: OtherDate), Teacher(teacherContractId: 6, salarySchemaId: 4, startDate: OtherDate)
        ], students: [Student(200, 22, 8, 60, 7.5m), Student(0, 23)], dayTimePlaces: [DayTimePlace(weekDayId: 4)]);

        Result result = await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("1002", group.GroupCode);
        Assert.True(group.DirtyLessons);
        Assert.Equal([OtherDate, null], group.GroupsByTeachers.Select(t => t.EndDate));
        Assert.Equal([5, 6], group.GroupsByTeachers.Select(t => t.TeacherContractId));
        GroupByStudent moved = group.GroupsByStudents.Single(s => s.GbsId == 200);
        Assert.Equal(22, moved.StudentContractId);
        Assert.Equal(60m, moved.FourWeekFee);
        Assert.Equal(7.5m, moved.OneHourFee);
        Assert.Contains(group.GroupsByStudents, s => s is { GbsId: 0, StudentContractId: 23 });
        Assert.NotNull(removed);
        Assert.Empty(removed.Teachers);
        Assert.Equal([201], removed.Students.Select(s => s.GbsId));
        Assert.Equal([300], removed.DayTimePlaces.Select(d => d.GdtpId));
        Assert.Contains(group.GroupDayTimePlaces, d => d is { GdtpId: 0, WeekDayId: 4 });
        //the old contracts of the moved and the removed row, and the new ones
        AssertLoadedContractsAreDirty(20, 21, 22, 23);
        VerifySaved(Times.Once());
    }

    // a save without changes still asks the lesson generator to check the group again
    [Fact]
    public async Task Update_SameData_StillMarksGroupAndItsStudentsDirty()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        UpdateGroupCommandHandler handler = UpdateHandler();
        GroupRequest request = ValidRequest(teachers: [Teacher(100)], students: [Student(200), Student(201, 21)],
            dayTimePlaces: [DayTimePlace(300)]);

        Result result = await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(group.DirtyLessons);
        AssertLoadedContractsAreDirty(20, 21);
        _repository.Verify(
            r => r.AnyStudentRowIsInUse(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_RemovingAStudentWithLessons_IsConflictWithoutChanges()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        _repository.Setup(r => r.AnyStudentRowIsInUse(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(201)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        UpdateGroupCommandHandler handler = UpdateHandler();
        GroupRequest request = ValidRequest("1099", teachers: [Teacher(100)], students: [Student(200)],
            dayTimePlaces: [DayTimePlace(300)]);

        Result result = await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.Equal(GroupErrors.GroupStudentIsInUse.Code, result.Error.Code);
        Assert.Equal("1001", group.GroupCode);
        Assert.False(group.DirtyLessons);
        Assert.Empty(_loadedContracts);
        _repository.Verify(r => r.RemoveRows(It.IsAny<GroupRemovedRows>()), Times.Never);
        VerifySaved(Times.Never());
    }

    // a teacher changed to another contract without a scheme gets the new contract's default scheme
    [Fact]
    public async Task Update_TeacherWithoutScheme_GetsTheContractsDefaultScheme()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        _repository.Setup(r => r.GetDefaultSalarySchemeId(6, It.IsAny<CancellationToken>())).ReturnsAsync(15);
        UpdateGroupCommandHandler handler = UpdateHandler();
        GroupRequest request = ValidRequest(teachers: [Teacher(100, 6, null)], students: [Student(200), Student(201)],
            dayTimePlaces: [DayTimePlace(300)]);

        await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        GroupByTeacher teacher = Assert.Single(group.GroupsByTeachers);
        Assert.Equal(6, teacher.TeacherContractId);
        Assert.Equal(15, teacher.SalarySchemaId);
    }

    //D177: the lesson of 21.09 with attendance would start after the new void date, so the generator could not
    //delete it (error 11 of report r13)
    [Fact]
    public async Task Update_VoidDateBeforeALessonWithAttendance_IsConflictWithoutChanges()
    {
        // Arrange
        Group group = ExistingGroup();
        WithExistingGroup(group);
        WithSavedGroup(SavedGroup(null, AttendedLesson(500, Monday21.AddHours(15)),
            AttendedLesson(501, Monday21.AddDays(7).AddHours(15))));
        GroupRequest request = ValidRequest("1099", Monday21, [Teacher(100)], [Student(200), Student(201, 21)],
            [DayTimePlace(300)]);

        // Act
        Result result = await UpdateHandler().Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        // Assert
        Assert.Equal(nameof(GroupErrors.ChangeCreatesLessonsWithErrors), result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains(": 21.09.2026 15:00, 28.09.2026 15:00. ", result.Error.Description, StringComparison.Ordinal);
        Assert.Equal("1001", group.GroupCode);
        Assert.Null(group.VoidDate);
        Assert.False(group.DirtyLessons);
        Assert.Empty(_loadedContracts);
        _repository.Verify(r => r.RemoveRows(It.IsAny<GroupRemovedRows>()), Times.Never);
        VerifySaved(Times.Never());
    }

    //D57 lets a student row move to another contract, but the attended row of the old contract would stay in the
    //lesson as an extra student (error 14)
    [Fact]
    public async Task Update_AttendedStudentMovedToAnotherContract_IsConflict()
    {
        WithExistingGroup(ExistingGroup());
        WithSavedGroup(SavedGroup(null, AttendedLesson(500, Monday21.AddHours(15))));
        GroupRequest request = ValidRequest(teachers: [Teacher(100)], students: [Student(200, 22), Student(201, 21)],
            dayTimePlaces: [DayTimePlace(300)]);

        Result result = await UpdateHandler().Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.Equal(nameof(GroupErrors.ChangeCreatesLessonsWithErrors), result.Error.Code);
        Assert.Contains(": 21.09.2026 15:00. ", result.Error.Description, StringComparison.Ordinal);
        VerifySaved(Times.Never());
    }

    //the lesson of 21.09 was already after the void date: the error is in report r13 and another change is saved
    [Fact]
    public async Task Update_LessonThatWasAlreadyErroneous_DoesNotStopTheSave()
    {
        Group group = ExistingGroup();
        group.VoidDate = Monday21;
        WithExistingGroup(group);
        WithSavedGroup(SavedGroup(Monday21, AttendedLesson(500, Monday21.AddHours(15))));
        GroupRequest request = ValidRequest("1002", Monday21, [Teacher(100)], [Student(200), Student(201, 21)],
            [DayTimePlace(300)]);

        Result result = await UpdateHandler().Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("1002", group.GroupCode);
        VerifySaved(Times.Once());
    }

    //the lesson without attendance is simply deleted by the generator
    [Fact]
    public async Task Update_VoidDateBeforeALessonWithoutEnteredData_IsSaved()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        Lesson lesson = AttendedLesson(500, Monday21.AddHours(15));
        lesson.LessonsByStudents.Single().Present = false;
        WithSavedGroup(SavedGroup(null, lesson));
        GroupRequest request = ValidRequest(voidDate: Monday21, teachers: [Teacher(100)],
            students: [Student(200), Student(201, 21)], dayTimePlaces: [DayTimePlace(300)]);

        Result result = await UpdateHandler().Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Monday21, group.VoidDate);
        VerifySaved(Times.Once());
    }

    //D178: the saved group already overlapped group 7 on Mondays; any save of it is refused until that is fixed
    [Fact]
    public async Task Update_TeacherTimesOverlap_IsConflictWithoutChangesEvenWhenItWasAlreadySaved()
    {
        // Arrange
        Group group = ExistingGroup();
        WithExistingGroup(group);
        WithOtherGroupOfTeacher5();
        GroupRequest request = ValidRequest("1002", teachers: [Teacher(100)],
            students: [Student(200), Student(201, 21)], dayTimePlaces: [DayTimePlace(300)]);

        // Act
        Result result = await UpdateHandler().Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        // Assert
        Assert.Equal(nameof(GroupErrors.TeacherTimesOverlap), result.Error.Code);
        Assert.Contains(": Alpha Ann / T5 — ჯგუფი 1002, ორშაბათი 15:30–17:00, 15.09.2026-დან. ",
            result.Error.Description, StringComparison.Ordinal);
        VerifyTeacherSchedulesOfContract5Except(42);
        Assert.Equal("1001", group.GroupCode);
        Assert.False(group.DirtyLessons);
        Assert.Empty(_loadedContracts);
        _repository.Verify(r => r.RemoveRows(It.IsAny<GroupRemovedRows>()), Times.Never);
        VerifySaved(Times.Never());
    }

    //the request's schedule is checked, not the saved one: the lesson moves to Wednesdays, when group 7 has none
    [Fact]
    public async Task Update_ScheduleMovedAwayFromTheOtherGroup_IsSaved()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        WithOtherGroupOfTeacher5();
        GroupRequest request = ValidRequest(teachers: [Teacher(100)], students: [Student(200), Student(201, 21)],
            dayTimePlaces: [DayTimePlace(300, 3)]);

        Result result = await UpdateHandler().Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, Assert.Single(group.GroupDayTimePlaces).WeekDayId);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        var handler = new DeleteGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteGroupCommand(42), CancellationToken.None);

        Assert.Equal(GroupErrors.GroupNotFound.Code, result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Delete_GroupWithLessons_IsConflictAndNothingIsRemoved()
    {
        WithExistingGroup(ExistingGroup());
        _repository.Setup(r => r.IsInUse(42, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new DeleteGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteGroupCommand(42), CancellationToken.None);

        Assert.Equal(GroupErrors.GroupIsInUse.Code, result.Error.Code);
        Assert.Empty(_loadedContracts);
        _repository.Verify(r => r.Remove(It.IsAny<Group>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Delete_RemovesTheGroupAndMarksItsStudentsContractsDirty()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        var handler = new DeleteGroupCommandHandler(_repository.Object, _unitOfWork.Object);

        Result result = await handler.Handle(new DeleteGroupCommand(42), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(group), Times.Once);
        AssertLoadedContractsAreDirty(20, 21);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task GetGroup_NotFound_ReturnsNotFound()
    {
        var handler = new GetGroupQueryHandler(_repository.Object);

        Result<GroupResponse> result = await handler.Handle(new GetGroupQuery(7), CancellationToken.None);

        Assert.Equal(GroupErrors.GroupNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetGroup_Found_ReturnsIt()
    {
        var response = new GroupResponse(7, 11, "1001", 6, 2, 10, null, true, [], [], []);
        _repository.Setup(r => r.GetOne(7, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        var handler = new GetGroupQueryHandler(_repository.Object);

        Result<GroupResponse> result = await handler.Handle(new GetGroupQuery(7), CancellationToken.None);

        Assert.Same(response, result.Value);
    }

    [Fact]
    public async Task GetStudentContracts_ReturnsTheContractsOfTheYear()
    {
        List<GroupStudentContractLookupResponse> contracts = [new(20, "A B / 6.001", [])];
        _repository.Setup(r => r.GetStudentContracts(11, It.IsAny<CancellationToken>())).ReturnsAsync(contracts);
        var handler = new GetGroupStudentContractsQueryHandler(_repository.Object);

        Result<List<GroupStudentContractLookupResponse>> result =
            await handler.Handle(new GetGroupStudentContractsQuery(11), CancellationToken.None);

        Assert.Same(contracts, result.Value);
    }

    [Fact]
    public async Task FormLookups_CombinesTheLookupsAndComputesTheCurrentYear()
    {
        var studentContracts = new Mock<IStudentContractsRepository>();
        studentContracts.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
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
        List<LookupItemResponse> courses = [new(1, "c")];
        List<LookupItemResponse> groupSizes = [new(2, "g")];
        List<LookupItemResponse> statuses = [new(3, "s")];
        List<LookupItemResponse> schemes = [new(4, "h")];
        List<GroupTeacherContractLookupResponse> teacherContracts = [new(5, "t", 4)];
        List<LookupItemResponse> weekDays = [new(6, "w")];
        List<LookupItemResponse> times = [new(7, "08:00")];
        List<LookupItemResponse> rooms = [new(8, "r")];
        studentContracts.Setup(r => r.GetCourses(It.IsAny<CancellationToken>())).ReturnsAsync(courses);
        studentContracts.Setup(r => r.GetGroupSizes(It.IsAny<CancellationToken>())).ReturnsAsync(groupSizes);
        studentContracts.Setup(r => r.GetStudentStatuses(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);
        var teacherContractsRepository = new Mock<ITeacherContractsRepository>();
        teacherContractsRepository.Setup(r => r.GetSalarySchemes(It.IsAny<CancellationToken>())).ReturnsAsync(schemes);
        _repository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>())).ReturnsAsync(teacherContracts);
        _repository.Setup(r => r.GetWeekDays(It.IsAny<CancellationToken>())).ReturnsAsync(weekDays);
        _repository.Setup(r => r.GetLessonStartTimes(It.IsAny<CancellationToken>())).ReturnsAsync(times);
        _repository.Setup(r => r.GetRooms(It.IsAny<CancellationToken>())).ReturnsAsync(rooms);
        var handler = new GetGroupFormLookupsQueryHandler(_repository.Object, studentContracts.Object,
            teacherContractsRepository.Object,
            TimeProviderAt(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero)));

        Result<GroupFormLookupsResponse> result =
            await handler.Handle(new GetGroupFormLookupsQuery(), CancellationToken.None);

        GroupFormLookupsResponse lookups = result.Value;
        Assert.Equal(11, lookups.CurrentAcademicYearId);
        Assert.Equal([10, 11], lookups.AcademicYears.Select(y => y.Id));
        Assert.Equal("2025-2026", lookups.AcademicYears[0].Name);
        Assert.Same(courses, lookups.Courses);
        Assert.Same(groupSizes, lookups.GroupSizes);
        Assert.Same(statuses, lookups.StudentStatuses);
        Assert.Same(teacherContracts, lookups.TeacherContracts);
        Assert.Same(schemes, lookups.SalarySchemes);
        Assert.Same(weekDays, lookups.WeekDays);
        Assert.Same(times, lookups.LessonStartTimes);
        Assert.Same(rooms, lookups.Rooms);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
    public async Task RowsData_UndecodableRequest_Fails(string filterSortRequest)
    {
        var handler = new GetGroupsRowsDataQueryHandler(_repository.Object, TimeProvider.System);

        Result<GroupsRowsDataResponse> result =
            await handler.Handle(new GetGroupsRowsDataQuery(filterSortRequest), CancellationToken.None);

        Assert.Equal(GroupErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task RowsData_NullJson_Fails()
    {
        var handler = new GetGroupsRowsDataQueryHandler(_repository.Object, TimeProvider.System);

        Result<GroupsRowsDataResponse> result =
            await handler.Handle(new GetGroupsRowsDataQuery(Encode("null")), CancellationToken.None);

        Assert.Equal(GroupErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task RowsData_InvalidFilter_FailsWithoutQueryingRepository()
    {
        var handler = new GetGroupsRowsDataQueryHandler(_repository.Object, TimeProvider.System);
        string json = """{"offset":0,"rowsCount":10,"filterFields":[{"fieldName":"findMethod","value":"room"}]}""";

        Result<GroupsRowsDataResponse> result =
            await handler.Handle(new GetGroupsRowsDataQuery(Encode(json)), CancellationToken.None);

        Assert.True(result.IsFailure);
        _repository.Verify(r => r.GetRowsData(It.IsAny<GroupsListQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RowsData_ValidRequest_PassesTypedQueryWithTodayToRepository()
    {
        var expected = new GroupsRowsDataResponse(0, 0, []);
        GroupsListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<GroupsListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GroupsListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(expected);
        var handler = new GetGroupsRowsDataQueryHandler(_repository.Object,
            TimeProviderAt(new DateTimeOffset(2026, 9, 30, 17, 45, 0, TimeSpan.Zero)));
        //the front end URL-encodes the JSON before base64 (Georgian text), the factory decodes it
        string json = Uri.EscapeDataString(
            """{"offset":10,"rowsCount":10,"filterFields":[{"fieldName":"findMethod","value":"student"},{"fieldName":"search","value":"ბერიძე"}],"sortByFields":[{"fieldName":"endDate","ascending":false}]}""");

        Result<GroupsRowsDataResponse> result =
            await handler.Handle(new GetGroupsRowsDataQuery(Encode(json)), CancellationToken.None);

        Assert.Same(expected, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified), passed.Today);
        Assert.Equal(EGroupFindMethod.Student, passed.FindMethod);
        Assert.Equal("ბერიძე", passed.Search);
        Assert.Equal([new GroupSortField(EGroupSortField.EndDate, false)], passed.SortFields);
    }
}
