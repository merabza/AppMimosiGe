using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Rights;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SharedKernel;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.Infrastructure.Rights;

//იგივე შემოწმება, რასაც carcass-ის UserClaimRightsFilter აკეთებს: მომხმარებლის რომელიმე როლს აქვს თუ არა AppClaim
public sealed class UserClaimRights(
    IUserRightsRepository repository,
    ICurrentUser currentUser,
    IDatabaseAbstraction databaseAbstraction,
    ILogger<UserClaimRights> logger) : IUserClaimRights
{
    public async Task<bool> HasClaim(string claimKey, CancellationToken cancellationToken = default)
    {
        var rightsDeterminer = new RightsDeterminer(repository, logger, currentUser, databaseAbstraction);
        Result<bool> result = await rightsDeterminer.CheckUserRightToClaim(claimKey, cancellationToken);

        //უფლება ვერ დადგინდა (მიზეზს RightsDeterminer ლოგში წერს): უფლება არ არის
        return result.IsSuccess && result.Value;
    }
}
