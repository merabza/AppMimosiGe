using CarcassRepositories;
using MimosiGeDb;

namespace AppMimosiGeRepositories;

public sealed class MimosiGeMasterDataRepository : CarcassMasterDataRepository
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public MimosiGeMasterDataRepository(MimosiGeDbContext context) : base(context)
    {
    }
}