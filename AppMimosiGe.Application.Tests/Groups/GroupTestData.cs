using System;
using System.Collections.Generic;
using System.Threading;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.V1.Requests;
using Moq;

namespace AppMimosiGe.Application.Tests.Groups;

internal static class GroupTestData
{
    //the default salary scheme of every teacher contract in RepositoryWhereEverythingExists
    public const int DefaultSalarySchemeId = 9;
    public static readonly DateTime StartDate = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified);

    public static GroupRequest ValidRequest(string? groupCode = "1001", DateTime? voidDate = null,
        List<GroupTeacherRequest>? teachers = null, List<GroupStudentRequest>? students = null,
        List<GroupDayTimePlaceRequest>? dayTimePlaces = null)
    {
        return new GroupRequest
        {
            AcademicYearId = 11,
            GroupCode = groupCode,
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            VoidDate = voidDate,
            Teachers = teachers ?? [Teacher()],
            Students = students ?? [Student()],
            DayTimePlaces = dayTimePlaces ?? [DayTimePlace()]
        };
    }

    public static GroupTeacherRequest Teacher(int id = 0, int teacherContractId = 5, int? salarySchemaId = 8,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        return new GroupTeacherRequest
        {
            Id = id,
            TeacherContractId = teacherContractId,
            SalarySchemaId = salarySchemaId,
            StartDate = startDate ?? StartDate,
            EndDate = endDate
        };
    }

    public static GroupStudentRequest Student(int id = 0, int studentContractId = 20, float fourWeekHours = 12,
        decimal fourWeekFee = 72, decimal oneHourFee = 6, float hoursCoefficient = 1, DateTime? startDate = null,
        DateTime? endDate = null, string? note = null)
    {
        return new GroupStudentRequest
        {
            Id = id,
            StudentContractId = studentContractId,
            FourWeekHours = fourWeekHours,
            FourWeekFee = fourWeekFee,
            OneHourFee = oneHourFee,
            HoursCoefficient = hoursCoefficient,
            StartDate = startDate ?? StartDate,
            EndDate = endDate,
            Note = note
        };
    }

    public static GroupDayTimePlaceRequest DayTimePlace(int id = 0, int weekDayId = 1, int lessonStartTimeId = 17,
        float hoursCount = 1.5f, int roomId = 2, DateTime? startDate = null, DateTime? endDate = null)
    {
        return new GroupDayTimePlaceRequest
        {
            Id = id,
            WeekDayId = weekDayId,
            LessonStartTimeId = lessonStartTimeId,
            HoursCount = hoursCount,
            RoomId = roomId,
            StartDate = startDate ?? StartDate,
            EndDate = endDate
        };
    }

    //every referenced record exists, every teacher contract has a default scheme and no group code is taken
    public static Mock<IGroupsRepository> RepositoryWhereEverythingExists()
    {
        var repository = new Mock<IGroupsRepository>();
        repository.Setup(r => r.TeacherContractExists(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository.Setup(r => r.StudentContractExists(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository.Setup(r => r.WeekDayExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.LessonStartTimeExists(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository.Setup(r => r.RoomExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetDefaultSalarySchemeId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultSalarySchemeId);
        repository.Setup(r =>
                r.GroupCodeExists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return repository;
    }

    public static Mock<IStudentContractsRepository> StudentContractsRepositoryWhereEverythingExists()
    {
        var repository = new Mock<IStudentContractsRepository>();
        repository.Setup(r => r.AcademicYearExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.CourseExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GroupSizeExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.StudentStatusExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return repository;
    }

    public static Mock<ITeacherContractsRepository> TeacherContractsRepositoryWhereEverythingExists()
    {
        var repository = new Mock<ITeacherContractsRepository>();
        repository.Setup(r => r.SalarySchemeExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return repository;
    }
}
