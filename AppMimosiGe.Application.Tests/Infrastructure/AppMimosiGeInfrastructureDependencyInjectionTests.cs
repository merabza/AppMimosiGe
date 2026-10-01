using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Infrastructure.DependencyInjection;
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
