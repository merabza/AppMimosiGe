using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppMimosiGe.Infrastructure.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class AppMimosiGeInfrastructureDependencyInjection
{
    public static IServiceCollection AddAppMimosiGeInfrastructure(this IServiceCollection services,
        ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(AddAppMimosiGeInfrastructure));

        services.AddScoped<IStudentContractsRepository, StudentContractsRepository>();
        services.AddScoped<ITeacherContractsRepository, TeacherContractsRepository>();

        debugLogger?.Information("{MethodName} Finished", nameof(AddAppMimosiGeInfrastructure));

        return services;
    }
}
