using System;
using System.Threading;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.V1.Requests;
using Moq;

namespace AppMimosiGe.Application.Tests.TeacherContracts;

internal static class TeacherContractTestData
{
    public static readonly DateTime ContractDate = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified);

    public static TeacherContractRequest ValidRequest(string? contractNumber = "T3.01", int? rsQuoteTypeId = 1,
        int? salarySchemaByHoursId = 4, int? workHourGroupId = 5, TimeOnly? workHoursStart = null,
        TimeOnly? workHoursEnd = null, DateTime? contractEndDate = null, decimal fixedAmount = 0,
        string? bankAccount = "GE00TB0000000000000000", string? bankAccountCode = "TBCBGE22",
        string? description = null)
    {
        return new TeacherContractRequest
        {
            ContractNumber = contractNumber,
            ContractDate = ContractDate,
            TeacherHumanId = 1,
            BankAccount = bankAccount,
            BankAccountCode = bankAccountCode,
            PensionScheme = true,
            IndEnt = false,
            RsQuoteTypeId = rsQuoteTypeId,
            RsCountryId = 2,
            FixedAmount = fixedAmount,
            NextMonth = true,
            Description = description,
            SalarySchemaByHoursId = salarySchemaByHoursId,
            WorkHourGroupId = workHourGroupId,
            WorkHoursStart = workHoursStart,
            WorkHoursEnd = workHoursEnd,
            ContractEndDate = contractEndDate
        };
    }

    //every referenced record exists and no contract number is taken
    public static Mock<ITeacherContractsRepository> RepositoryWhereEverythingExists()
    {
        var repository = new Mock<ITeacherContractsRepository>();
        repository.Setup(r => r.HumanExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.RsQuoteTypeExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.RsCountryExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.SalarySchemeExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.WorkHourGroupExists(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository.Setup(r =>
                r.ContractNumberExists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return repository;
    }
}
