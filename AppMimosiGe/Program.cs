//Created by ApiProgramClassCreator at 2/15/2025 11:07:44 AM

using System;
using System.Reflection;
using AppMimosiGeRepositories.DependencyInjection;
using BackendCarcass.Api.DependencyInjection;
using BackendCarcass.Application.DependencyInjection;
using BackendCarcass.Identity.DependencyInjection;
using BackendCarcass.Repositories.DependencyInjection;
using ConfigurationEncrypt;
using CorsTools.DependencyInjection;
using Figgle.Fonts;
using MediatorTools.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using MimosiGeDb.DependencyInjection;
using Serilog;
using SerilogLogger;
using SwaggerTools.DependencyInjection;
using TestToolsApi.DependencyInjection;
using WindowsServiceTools;

//using AssemblyReference = CarcassRepositories.AssemblyReference;

try
{
    Console.WriteLine("Loading...");

    const string appName = "App.Mimosi.Ge";
    const string appKey = "01e719dc51534a83988741c50da6a87b";
    const int versionCount = 1;

    var header = $"{appName} {Assembly.GetEntryAssembly()?.GetName().Version}";
    Console.WriteLine(FiggleFonts.Standard.Render(header));

    //var parameters = new Dictionary<string, string>
    //{
    //    { ConfigurationEncryptInstaller.AppKeyKey, "01e719dc51534a83988741c50da6a87b" },
    //    { SwaggerInstaller.AppNameKey, "App Mimosi Ge" },
    //    { SwaggerInstaller.VersionCountKey, 1.ToString() },
    //    { SwaggerInstaller.UseSwaggerWithJwtBearerKey, string.Empty } //Allow Swagger
    //};

    var builder =
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory, Args = args
        });

    var debugMode = builder.Environment.IsDevelopment();

    var logger = builder.Host.UseSerilogLogger(builder.Configuration, debugMode);
    builder.Host.UseWindowsServiceOnWindows(logger, debugMode, args);
    builder.Configuration.AddConfigurationEncryption(logger, debugMode, appKey);

    //if (!builder.InstallServices(debugMode, args, parameters,

    //        //BackendCarcass
    //        AssemblyReference.Assembly, BackendCarcassApi.AssemblyReference.Assembly,
    //        CarcassDom.AssemblyReference.Assembly, CarcassIdentity.AssemblyReference.Assembly,

    //        //AppMimosiGeDbPart
    //        MimosiGeDb.AssemblyReference.Assembly, AppMimosiGeRepositories.AssemblyReference.Assembly,

    //        //WebSystemTools
    //        ApiExceptionHandler.AssemblyReference.Assembly, ConfigurationEncrypt.AssemblyReference.Assembly,
    //        CorsTools.AssemblyReference.Assembly, SerilogLogger.AssemblyReference.Assembly,
    //        StaticFilesTools.AssemblyReference.Assembly, SwaggerTools.AssemblyReference.Assembly,
    //        TestToolsApi.AssemblyReference.Assembly, WindowsServiceTools.AssemblyReference.Assembly))
    //    return 2;

    //var mediatRSettings = builder.Configuration.GetSection("MediatRLicenseKey");

    //var mediatRLicenseKey = mediatRSettings.Get<string>();

    //builder.Services.AddMediatR(cfg =>
    //{
    //    cfg.LicenseKey = mediatRLicenseKey;
    //    //BackendCarcass
    //    cfg.RegisterServicesFromAssembly(BackendCarcassApi.AssemblyReference.Assembly);
    //    //AppMimosiGe
    //});

    ////FluentValidationInstaller

    //builder.Services.InstallValidation(
    //    //BackendCarcass
    //    BackendCarcassApi.AssemblyReference.Assembly
    //    //AppMimosiGe
    //);

    // @formatter:off
    builder.Services
        .AddMediator(builder.Configuration, debugMode)
        .AddSwagger(debugMode, true, versionCount, appName)
        .AddCorsService(logger, builder.Configuration, debugMode)
        .AddCarcassRepositories(logger, debugMode)
        .AddCarcassIdentity(logger, builder.Configuration, debugMode)
        .AddScopedAllCarcassApplicationServices(logger, debugMode)
        //.AddCarcassDom(debugMode)
        .AddAppMimosiGeRepositories(logger, debugMode)
        .AddMimosiGeDb(builder.Configuration,debugMode);
    // @formatter:on

    //ReSharper disable once using
    await using var app = builder.Build();

    // ReSharper disable once RedundantArgumentDefaultValue
    app.UseSwaggerServices(debugMode, versionCount);
    app.UseTestToolsApiEndpoints(debugMode);

    app.UseBackendCarcassApiEndpoints(logger, debugMode);

    //app.UseModelEditorApi(debugMode);
    //app.UseArticlesApiEndpoints(debugMode);
    //app.UseIssuesApiEndpoints(debugMode);
    //app.UseRootDerivationInflectionViewApi(debugMode);
    //app.UseRootsEditorApi(debugMode);

    await app.RunAsync();
    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
