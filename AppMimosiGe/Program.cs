//Created by ApiProgramClassCreator at 2/15/2025 11:07:44 AM

using System;
using System.Reflection;
using AppMimosiGeRepositories.DependencyInjection;
using BackendCarcassApi;
using BackendCarcassApi.DependencyInjection;
using Carcass.Application.DependencyInjection;
using CarcassIdentity.DependencyInjection;
using CarcassRepositories.DependencyInjection;
using ConfigurationEncrypt;
using CorsTools.DependencyInjection;
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
    //Console.WriteLine(FiggleFonts.Standard.Render(header));

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

    builder.Host.UseSerilogLogger(builder.Configuration, debugMode);
    builder.Host.UseWindowsServiceOnWindows(debugMode, args);

    builder.Configuration.AddConfigurationEncryption(debugMode, appKey);

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
        //SystemTools
        //.AddReCounterDom(debugMode) //ReCounter
                                    //WebSystemTools
        .AddSwagger(debugMode, true, versionCount, appName)
        .AddCorsService(builder.Configuration, debugMode)
        //.AddFluentValidation(
        //    debugMode,
        //    AssemblyReference.Assembly,
        //    ModelEditorApi.AssemblyReference.Assembly,
        //    RootsEditorApi.AssemblyReference.Assembly)
        .AddMediator(
            builder.Configuration,
            debugMode,
            AssemblyReference.Assembly)
            //SignalRRecounterMessages.AssemblyReference.Assembly,
            //DataBaseReCounterApi.AssemblyReference.Assembly,
            //IssuesApi.AssemblyReference.Assembly,
            //ModelEditorApi.AssemblyReference.Assembly,
            //RootDerivationInflectionViewApi.AssemblyReference.Assembly,
            //RootsEditorApi.AssemblyReference.Assembly)
        //.AddSignalRRecounterMessages(debugMode)  //ReCounter
                                                 //BackendCarcass
        .AddCarcassRepositories(debugMode)
        .AddCarcassIdentity(builder.Configuration, debugMode)
        .AddScopedAllCarcassApplicationServices(debugMode)
        //.AddCarcassDom(debugMode)
        .AddAppMimosiGeRepositories(debugMode)
        .AddMimosiGeDb(builder.Configuration,debugMode);
    //GrammarGeDbPart
    //.AddGeoModelDatabaseRepositories(debugMode)
    //.AddGrammarGeDb(builder.Configuration, debugMode)
    //AppGrammarGe
    //.AddLModel(x =>
    //{
    //    x.UseReCounters = true;
    //    x.UseObservers = true;
    //})
    //.AddModelEditorRepositories(debugMode)
    //.AddAppGrammarGeRepositories(debugMode)
    //.AddDataBaseReCounterRepositories(debugMode)
    //.AddIssuesRepositories(debugMode)
    //.AddRootDerivationInflectionRepositories(debugMode)
    //.AddRootsEditorRepositories(debugMode);

    // @formatter:on

    //ReSharper disable once using
    using var app = builder.Build();

    //if (!app.UseServices(debugMode)) return 1;

    // ReSharper disable once RedundantArgumentDefaultValue
    app.UseSwaggerServices(debugMode, versionCount);
    app.UseTestToolsApiEndpoints(debugMode);
    //app.UseSignalRRecounterMessages(debugMode);

    app.UseBackendCarcassApiEndpoints(debugMode);

    //app.UseModelEditorApi(debugMode);
    //app.UseArticlesApiEndpoints(debugMode);
    //app.UseIssuesApiEndpoints(debugMode);
    //app.UseRootDerivationInflectionViewApi(debugMode);
    //app.UseRootsEditorApi(debugMode);

    app.Run();
    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}