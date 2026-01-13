//Created by RepositoryInterfaceCreator at 2/15/2025 11:07:44 AM

using System;
using Microsoft.EntityFrameworkCore.Storage;

namespace LibAppMimosiGeRepositories;

public interface IAppMimosiGeRepository
{
    int SaveChanges();
    int SaveChangesWithTransaction();
    IDbContextTransaction GetTransaction();
}
