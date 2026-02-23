using BackendCarcass.Repositories;
using MimosiGeDbPart.Db;

namespace AppMimosiGeRepositories;

public sealed class MimosiGeMasterDataRepository : CarcassMasterDataRepository
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public MimosiGeMasterDataRepository(MimosiGeDbContext context) : base(context)
    {
    }
}
