//Created by ApiProgramClassCreator at 2/15/2025 11:07:44 AM

using System;
using System.Collections.Generic;
using ConfigurationEncrypt;
using FluentValidationInstaller;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SwaggerTools;
using WebInstallers;
using AssemblyReference = CarcassRepositories.AssemblyReference;

try
{
    var parameters = new Dictionary<string, string>
    {
        { ConfigurationEncryptInstaller.AppKeyKey, "01e719dc51534a83988741c50da6a87b" },
        { SwaggerInstaller.AppNameKey, "App Mimosi Ge" },
        { SwaggerInstaller.VersionCountKey, 1.ToString() },
        { SwaggerInstaller.UseSwaggerWithJwtBearerKey, string.Empty } //Allow Swagger
    };

    var builder =
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory, Args = args
        });

    var debugMode = builder.Environment.IsDevelopment();

    if (!builder.InstallServices(debugMode, args, parameters,

            //BackendCarcass
            AssemblyReference.Assembly, BackendCarcassApi.AssemblyReference.Assembly,
            CarcassDom.AssemblyReference.Assembly, CarcassIdentity.AssemblyReference.Assembly,

            //AppMimosiGeDbPart
            MimosiGeDb.AssemblyReference.Assembly, AppMimosiGeRepositories.AssemblyReference.Assembly,

            //WebSystemTools
            ApiExceptionHandler.AssemblyReference.Assembly, ConfigurationEncrypt.AssemblyReference.Assembly,
            CorsTools.AssemblyReference.Assembly, SerilogLogger.AssemblyReference.Assembly,
            StaticFilesTools.AssemblyReference.Assembly, SwaggerTools.AssemblyReference.Assembly,
            TestToolsApi.AssemblyReference.Assembly, WindowsServiceTools.AssemblyReference.Assembly))
        return 2;

    var mediatRSettings = builder.Configuration.GetSection("MediatRLicenseKey");

    var mediatRLicenseKey = mediatRSettings.Get<string>();

    builder.Services.AddMediatR(cfg =>
    {
        cfg.LicenseKey = mediatRLicenseKey;
        //BackendCarcass
        cfg.RegisterServicesFromAssembly(BackendCarcassApi.AssemblyReference.Assembly);
        //AppMimosiGe
    });

    //FluentValidationInstaller

    builder.Services.InstallValidation(
        //BackendCarcass
        BackendCarcassApi.AssemblyReference.Assembly
        //AppMimosiGe
    );

    //ReSharper disable once using

    using var app = builder.Build();

    if (!app.UseServices(debugMode)) return 1;

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