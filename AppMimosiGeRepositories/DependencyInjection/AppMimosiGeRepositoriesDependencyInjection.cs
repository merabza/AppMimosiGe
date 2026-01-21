//Created by RepositoriesInstallerClassCreator at 2/15/2025 11:07:44 AM

using BackendCarcass.MasterData;
using BackendCarcass.Repositories;
using BackendCarcass.Rights;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SystemTools.DomainShared.Repositories;

namespace AppMimosiGeRepositories.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class AppMimosiGeRepositoriesDependencyInjection
{
    public static IServiceCollection AddAppMimosiGeRepositories(this IServiceCollection services, ILogger logger,
        bool debugMode)
    {
        if (debugMode)
        {
            logger.Information("{MethodName} Started", nameof(AddAppMimosiGeRepositories));
        }

        services.AddScoped<IUserRightsRepository, UserRightsRepository>();
        services.AddScoped<IUnitOfWork, MimosiGeUnitOfWork>();
        services.AddScoped<IMasterDataLoaderCreator, MasterDataLoaderCreator>();
        services.AddScoped<IReturnValuesLoaderCreator, ReturnValuesLoaderCreator>();
        //builder.Services.AddScoped<IMasterDataLoaderCreator, MimMasterDataLoaderCrudCreator>()
        //builder.Services.AddScoped<IReturnValuesLoaderCreator, MimReturnValuesLoaderCreator>()
        services.AddScoped<ICarcassMasterDataRepository, MimosiGeMasterDataRepository>();

        if (debugMode)
        {
            logger.Information("{MethodName} Finished", nameof(AddAppMimosiGeRepositories));
        }

        return services;
    }
}
