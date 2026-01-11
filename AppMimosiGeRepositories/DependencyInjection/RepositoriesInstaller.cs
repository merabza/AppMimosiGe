//Created by RepositoriesInstallerClassCreator at 2/15/2025 11:07:44 AM

using System;
using CarcassMasterData;
using CarcassRepositories;
using CarcassRights;
using DomainShared.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AppMimosiGeRepositories.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class AppMimosiGeRepositoriesDependencyInjection
{
    public static IServiceCollection AddAppMimosiGeRepositories(this IServiceCollection services, bool debugMode)
    {
        if (debugMode) Console.WriteLine($"{nameof(AddAppMimosiGeRepositories)} Started");

        services.AddScoped<IUserRightsRepository, UserRightsRepository>();
        services.AddScoped<IUnitOfWork, MimosiGeUnitOfWork>();
        services.AddScoped<IMasterDataLoaderCreator, MasterDataLoaderCreator>();
        services.AddScoped<IReturnValuesLoaderCreator, ReturnValuesLoaderCreator>();
        //builder.Services.AddScoped<IMasterDataLoaderCreator, MimMasterDataLoaderCrudCreator>()
        //builder.Services.AddScoped<IReturnValuesLoaderCreator, MimReturnValuesLoaderCreator>()
        services.AddScoped<ICarcassMasterDataRepository, MimosiGeMasterDataRepository>();

        if (debugMode) Console.WriteLine($"{nameof(AddAppMimosiGeRepositories)} Started");

        return services;
    }
}