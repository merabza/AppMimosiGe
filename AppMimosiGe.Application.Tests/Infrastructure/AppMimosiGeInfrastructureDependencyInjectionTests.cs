using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Infrastructure.DependencyInjection;
using AppMimosiGe.Infrastructure.Repositories;
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
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(IStudentContractsRepository));
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
        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == typeof(ITeacherContractsRepository));
        Assert.Equal(typeof(TeacherContractsRepository), descriptor.ImplementationType);
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
        logger.Verify(l => l.Information("{MethodName} Started", nameof(AppMimosiGeInfrastructureDependencyInjection
            .AddAppMimosiGeInfrastructure)), Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(AppMimosiGeInfrastructureDependencyInjection
            .AddAppMimosiGeInfrastructure)), Times.Once);
    }
}
