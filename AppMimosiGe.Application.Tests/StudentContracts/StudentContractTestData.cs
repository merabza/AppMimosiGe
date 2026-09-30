using System;
using System.Threading;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGeShared.Contracts.V1.Requests;
using Moq;

namespace AppMimosiGe.Application.Tests.StudentContracts;

internal static class StudentContractTestData
{
    public static StudentContractRequest ValidRequest(string contractNumber = "6.001", int studentHumanId = 1,
        int payerHumanId = 2, int? studentStatusId = 3, int? desiredMonthlyPaymentDay = 10,
        StudentContractDetailRequest[]? details = null)
    {
        return new StudentContractRequest
        {
            ContractNumber = contractNumber,
            ContractDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified),
            StudentHumanId = studentHumanId,
            PayerHumanId = payerHumanId,
            AcademicYearId = 11,
            StudentStatusId = studentStatusId,
            DesiredMonthlyPaymentDay = desiredMonthlyPaymentDay,
            Details = details is null ? [Detail()] : [.. details]
        };
    }

    public static StudentContractDetailRequest Detail(int id = 0, int courseId = 5, int groupSizeId = 2,
        float fourWeekHours = 8, decimal fourWeekFee = 48, decimal oneHourFee = 6)
    {
        return new StudentContractDetailRequest
        {
            Id = id,
            CourseId = courseId,
            GroupSizeId = groupSizeId,
            FourWeekHours = fourWeekHours,
            FourWeekFee = fourWeekFee,
            OneHourFee = oneHourFee
        };
    }

    //every referenced record exists and no contract number is taken
    public static Mock<IStudentContractsRepository> RepositoryWhereEverythingExists()
    {
        var repository = new Mock<IStudentContractsRepository>();
        repository.Setup(r => r.HumanExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.AcademicYearExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.StudentStatusExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.CourseExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GroupSizeExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.ContractNumberExists(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);
        return repository;
    }
}
