//Created by RepositoryCreatorFabricCreator at 2/15/2025 11:07:44 AM

using System;
using Microsoft.Extensions.DependencyInjection;

namespace LibAppMimosiGeRepositories;

public sealed class AppMimosiGeRepositoryCreatorFabric : IAppMimosiGeRepositoryCreatorFabric
{
    private readonly IServiceProvider _services;

    public AppMimosiGeRepositoryCreatorFabric(IServiceProvider services)
    {
        _services = services;
    }

    public IAppMimosiGeRepository GetAppMimosiGeRepository()
    {
        var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAppMimosiGeRepository>();
    }
}