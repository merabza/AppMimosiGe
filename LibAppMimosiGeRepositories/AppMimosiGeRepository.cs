//Created by RepositoryClassCreator at 2/15/2025 11:07:44 AM

using System;
using AppMimosiGeDb;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LibAppMimosiGeRepositories;

public sealed class AppMimosiGeRepository : IAppMimosiGeRepository
{
    private readonly AppMimosiGeDbContext _context;
    private readonly ILogger<AppMimosiGeRepository> _logger;

    public AppMimosiGeRepository(AppMimosiGeDbContext ctx, ILogger<AppMimosiGeRepository> logger)
    {
        _context = ctx;
        _logger = logger;
    }

    public int SaveChanges()
    {
        try
        {
            return _context.SaveChanges();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error occurred executing {nameof(SaveChanges)}.");
            throw;
        }
    }

    public int SaveChangesWithTransaction()
    {
        try
        {
            using IDbContextTransaction transaction = GetTransaction();
            try
            {
                int ret = _context.SaveChanges();
                transaction.Commit();
                return ret;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error occurred executing {nameof(SaveChangesWithTransaction)}.");
            throw;
        }
    }

    public IDbContextTransaction GetTransaction()
    {
        return _context.Database.BeginTransaction();
    }
}