//Created by RepositoryCreatorFactoryCreator at 2/15/2025 11:07:44 AM

using System;
using Microsoft.Extensions.DependencyInjection;

namespace LibAppMimosiGeRepositories;

public sealed class AppMimosiGeRepositoryCreatorFactory : IAppMimosiGeRepositoryCreatorFactory
{
    private readonly IServiceProvider _services;

    public AppMimosiGeRepositoryCreatorFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IAppMimosiGeRepository GetAppMimosiGeRepository()
    {
        // ReSharper disable once using
        var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAppMimosiGeRepository>();
    }
}