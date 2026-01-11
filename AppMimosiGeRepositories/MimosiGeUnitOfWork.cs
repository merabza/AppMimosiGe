using Carcass.Persistence;
using MimosiGeDb;

namespace AppMimosiGeRepositories;

public class MimosiGeUnitOfWork : CarcassUnitOfWork
{
    public MimosiGeUnitOfWork(MimosiGeDbContext dbContext) : base(dbContext)
    {
    }
}