//Created by RepositoriesInstallerClassCreator at 2/15/2025 11:07:44 AM

using System;
using System.Collections.Generic;
using CarcassDom;
using CarcassMasterDataDom;
using CarcassRepositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RepositoriesDom;
using WebInstallers;

namespace AppMimosiGeRepositories.Installers;

// ReSharper disable once UnusedType.Global
public sealed class RepositoriesInstaller : IInstaller
{
    public int InstallPriority => 30;
    public int ServiceUsePriority => 30;

    public bool InstallServices(WebApplicationBuilder builder, bool debugMode, string[] args,
        Dictionary<string, string> parameters)
    {
        if (debugMode) Console.WriteLine($"{GetType().Name}.{nameof(InstallServices)} Started");

        builder.Services.AddScoped<IUserRightsRepository, UserRightsRepository>();
        builder.Services.AddScoped<IAbstractRepository, MimAbstractRepository>();
        builder.Services.AddScoped<IMasterDataLoaderCreator, MasterDataLoaderCreator>();
        builder.Services.AddScoped<IReturnValuesLoaderCreator, ReturnValuesLoaderCreator>();
        //builder.Services.AddScoped<IMasterDataLoaderCreator, MimMasterDataLoaderCrudCreator>()
        //builder.Services.AddScoped<IReturnValuesLoaderCreator, MimReturnValuesLoaderCreator>()

        if (debugMode) Console.WriteLine($"{GetType().Name}.{nameof(InstallServices)} Started");

        return true;
    }

    public bool UseServices(WebApplication app, bool debugMode)
    {
        return true;
    }
}