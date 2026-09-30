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

    //the student contracts the handler loaded to set their DirtyNextPayDate flag
    private readonly List<StudentContract> _loadedContracts = [];
    private readonly Mock<IGroupsRepository> _repository = RepositoryWhereEverythingExists();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public GroupHandlersTests()
    {
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
            Id = 100, GroupId = 42, TeacherContractId = 5, SalarySchemaId = 8, StartDate = StartDate
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
            GdtpId = 300, GroupId = 42, WeekDayId = 1, LessonStartTimeId = 17, RoomId = 2, StartDate = StartDate
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
        GroupRequest request = ValidRequest(" 1011 ", OtherDate.AddHours(10),
            [Teacher(endDate: OtherDate.AddHours(3))],
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
            Teacher(teacherContractId: 6, salarySchemaId: null, startDate: OtherDate,
                endDate: OtherDate.AddMonths(1)),
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
        _repository.Verify(r =>
            r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFoundAndDoesNotSave()
    {
        var handler = new UpdateGroupCommandHandler(_repository.Object, _unitOfWork.Object);

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
        var handler = new UpdateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
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
        var handler = new UpdateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
        //teacher 100 gets an end, a second teacher follows; student 200 moves to contract 22, student 201 leaves,
        //contract 23 joins; the schedule row 300 is replaced by a new one
        GroupRequest request = ValidRequest("1002", teachers:
            [
                Teacher(100, endDate: OtherDate), Teacher(teacherContractId: 6, salarySchemaId: 4, startDate: OtherDate)
            ], students: [Student(200, 22, fourWeekHours: 8, fourWeekFee: 60, oneHourFee: 7.5m), Student(0, 23)],
            dayTimePlaces: [DayTimePlace(weekDayId: 4)]);

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
        var handler = new UpdateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
        GroupRequest request = ValidRequest(teachers: [Teacher(100)], students: [Student(200), Student(201, 21)],
            dayTimePlaces: [DayTimePlace(300)]);

        Result result = await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(group.DirtyLessons);
        AssertLoadedContractsAreDirty(20, 21);
        _repository.Verify(r => r.AnyStudentRowIsInUse(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_RemovingAStudentWithLessons_IsConflictWithoutChanges()
    {
        Group group = ExistingGroup();
        WithExistingGroup(group);
        _repository.Setup(r => r.AnyStudentRowIsInUse(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(201)),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new UpdateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
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
        var handler = new UpdateGroupCommandHandler(_repository.Object, _unitOfWork.Object);
        GroupRequest request = ValidRequest(teachers: [Teacher(100, 6, null)], students: [Student(200), Student(201)],
            dayTimePlaces: [DayTimePlace(300)]);

        await handler.Handle(new UpdateGroupCommand(42, request), CancellationToken.None);

        GroupByTeacher teacher = Assert.Single(group.GroupsByTeachers);
        Assert.Equal(6, teacher.TeacherContractId);
        Assert.Equal(15, teacher.SalarySchemaId);
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
            teacherContractsRepository.Object, TimeProviderAt(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero)));

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
        _repository.Verify(r => r.GetRowsData(It.IsAny<GroupsListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
