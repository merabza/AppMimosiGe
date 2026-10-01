using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.CrmCalls;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGe.Infrastructure.Rights;
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
        services.AddScoped<IGroupsRepository, GroupsRepository>();
        services.AddScoped<ILessonGeneratorRepository, LessonGeneratorRepository>();
        services.AddScoped<ILessonsRepository, LessonsRepository>();
        services.AddScoped<IPaymentsRepository, PaymentsRepository>();
        services.AddScoped<IBalancesRepository, BalancesRepository>();
        services.AddScoped<ICrmCallsRepository, CrmCallsRepository>();
        services.AddScoped<IUserClaimRights, UserClaimRights>();

        debugLogger?.Information("{MethodName} Finished", nameof(AddAppMimosiGeInfrastructure));

        return services;
    }
}
