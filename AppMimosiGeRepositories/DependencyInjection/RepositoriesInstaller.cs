//Created by RepositoriesInstallerClassCreator at 2/15/2025 11:07:44 AM

using System;
using CarcassDom;
using CarcassMasterDataDom;
using CarcassRepositories;
using Microsoft.Extensions.DependencyInjection;
using RepositoriesDom;

namespace AppMimosiGeRepositories.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class AppMimosiGeRepositoriesDependencyInjection
{
    public static IServiceCollection AddAppMimosiGeRepositories(this IServiceCollection services, bool debugMode)
    {
        if (debugMode) Console.WriteLine($"{nameof(AddAppMimosiGeRepositories)} Started");

        services.AddScoped<IUserRightsRepository, UserRightsRepository>();
        services.AddScoped<IAbstractRepository, MimAbstractRepository>();
        services.AddScoped<IMasterDataLoaderCreator, MasterDataLoaderCreator>();
        services.AddScoped<IReturnValuesLoaderCreator, ReturnValuesLoaderCreator>();
        //builder.Services.AddScoped<IMasterDataLoaderCreator, MimMasterDataLoaderCrudCreator>()
        //builder.Services.AddScoped<IReturnValuesLoaderCreator, MimReturnValuesLoaderCreator>()
        services.AddScoped<ICarcassMasterDataRepository, MimosiGeMasterDataRepository>();

        if (debugMode) Console.WriteLine($"{nameof(AddAppMimosiGeRepositories)} Started");

        return services;
    }
}