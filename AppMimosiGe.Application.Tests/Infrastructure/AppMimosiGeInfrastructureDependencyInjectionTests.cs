using AppMimosiGe.Application.AcademicYears;
using System;
using System.Linq;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.CrmCalls;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.Salary;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Application.WorkHours;
using AppMimosiGe.Infrastructure.BankTransactions.BogBonline;
using AppMimosiGe.Infrastructure.BankTransactions.TbcBis;
using AppMimosiGe.Infrastructure.DependencyInjection;
using AppMimosiGe.Infrastructure.Reports;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGe.Infrastructure.Rights;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Serilog;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class AppMimosiGeInfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersStudentContractsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddAppMimosiGeInfrastructure(null);

        // Assert
        Assert.Same(services, result);
        ServiceDescriptor descriptor =
            Assert.Single(services, s => s.ServiceType == typeof(IStudentContractsRepository));
        Assert.Equal(typeof(StudentContractsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersTeacherContractsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor =
            Assert.Single(services, s => s.ServiceType == typeof(ITeacherContractsRepository));
        Assert.Equal(typeof(TeacherContractsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersGroupsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IGroupsRepository));
        Assert.Equal(typeof(GroupsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersLessonGeneratorRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor =
            Assert.Single(services, s => s.ServiceType == typeof(ILessonGeneratorRepository));
        Assert.Equal(typeof(LessonGeneratorRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersLessonsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(ILessonsRepository));
        Assert.Equal(typeof(LessonsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersPaymentsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IPaymentsRepository));
        Assert.Equal(typeof(PaymentsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersBankTransactionsRepositoryAsScoped()
    {
        var services = new ServiceCollection();

        services.AddAppMimosiGeInfrastructure(null);

        ServiceDescriptor descriptor =
            Assert.Single(services, s => s.ServiceType == typeof(IBankTransactionsRepository));
        Assert.Equal(typeof(BankTransactionsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // one client per bank for the process (each keeps its token), TBC first, built from the host's settings
    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersTheBankClientsAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        DateTime tbcStart = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
        DateTime bogStart = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified);

        services.AddAppMimosiGeInfrastructure(null, new TbcBisSettings { BankAccountId = 4, StartDate = tbcStart },
            new BogBonlineSettings { BankAccountId = 1, StartDate = bogStart });

        Assert.All(services.Where(s => s.ServiceType == typeof(IBankMovementsClient)),
            s => Assert.Equal(ServiceLifetime.Singleton, s.Lifetime));
        using ServiceProvider provider = services.BuildServiceProvider();
        IBankMovementsClient[] clients = [.. provider.GetServices<IBankMovementsClient>()];
        Assert.Equal([typeof(TbcBisClient), typeof(BogBonlineClient)], clients.Select(c => c.GetType()));
        Assert.Equal(clients, provider.GetServices<IBankMovementsClient>());
        Assert.Equal([new IntegratedBankAccount(4, tbcStart), new IntegratedBankAccount(1, bogStart)],
            clients.Select(c => c.Account));
    }

    // without the host's settings the clients are there, but not configured
    [Fact]
    public void AddAppMimosiGeInfrastructure_WithoutBankSettings_HasNoIntegratedAccount()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);

        services.AddAppMimosiGeInfrastructure(null);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal([null, null], provider.GetServices<IBankMovementsClient>().Select(c => c.Account));
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersBalancesRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IBalancesRepository));
        Assert.Equal(typeof(BalancesRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersCrmCallsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(ICrmCallsRepository));
        Assert.Equal(typeof(CrmCallsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersWorkHoursRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IWorkHoursRepository));
        Assert.Equal(typeof(WorkHoursRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersSalaryRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(ISalaryRepository));
        Assert.Equal(typeof(SalaryRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersReportsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IReportsRepository));
        Assert.Equal(typeof(ReportsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersAcademicYearsRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor =
            Assert.Single(services, s => s.ServiceType == typeof(IAcademicYearsRepository));
        Assert.Equal(typeof(AcademicYearsRepository), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // the Excel writer keeps no state, so one instance serves every request
    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersReportExcelWriterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IReportExcelWriter));
        Assert.Equal(typeof(OpenXmlReportExcelWriter), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    // the current user's roles are per request, so the claim check is scoped as well
    [Fact]
    public void AddAppMimosiGeInfrastructure_RegistersUserClaimRightsAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppMimosiGeInfrastructure(null);

        // Assert
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IUserClaimRights));
        Assert.Equal(typeof(UserClaimRights), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddAppMimosiGeInfrastructure_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        var logger = new Mock<ILogger>();

        // Act
        new ServiceCollection().AddAppMimosiGeInfrastructure(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started",
                nameof(AppMimosiGeInfrastructureDependencyInjection.AddAppMimosiGeInfrastructure)), Times.Once);
        logger.Verify(
            l => l.Information("{MethodName} Finished",
                nameof(AppMimosiGeInfrastructureDependencyInjection.AddAppMimosiGeInfrastructure)), Times.Once);
    }
}
