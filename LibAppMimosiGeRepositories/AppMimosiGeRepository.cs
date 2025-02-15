using System;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MimosiGeDb;

namespace LibAppMimosiGeRepositories;

public sealed class AppMimosiGeRepository : IAppMimosiGeRepository
{
    private readonly MimosiGeDbContext _context;
    private readonly ILogger<AppMimosiGeRepository> _logger;

    // ReSharper disable once ConvertToPrimaryConstructor
    public AppMimosiGeRepository(MimosiGeDbContext ctx, ILogger<AppMimosiGeRepository> logger)
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
            using var transaction = GetTransaction();
            try
            {
                var ret = _context.SaveChanges();
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