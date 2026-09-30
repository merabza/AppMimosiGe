using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.GetGroup;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupQueryHandler(IGroupsRepository repository) : IQueryHandler<GetGroupQuery, GroupResponse>
{
    public async Task<Result<GroupResponse>> Handle(GetGroupQuery request, CancellationToken cancellationToken)
    {
        GroupResponse? group = await repository.GetOne(request.GrpId, cancellationToken);
        return group is null ? GroupErrors.GroupNotFound : group;
    }
}
