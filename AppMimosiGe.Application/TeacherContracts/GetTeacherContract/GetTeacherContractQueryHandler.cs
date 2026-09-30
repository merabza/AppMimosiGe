using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.GetTeacherContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetTeacherContractQueryHandler(ITeacherContractsRepository repository)
    : IQueryHandler<GetTeacherContractQuery, TeacherContractResponse>
{
    public async Task<Result<TeacherContractResponse>> Handle(GetTeacherContractQuery request,
        CancellationToken cancellationToken)
    {
        TeacherContractResponse? teacherContract = await repository.GetOne(request.Id, cancellationToken);
        return teacherContract is null ? TeacherContractErrors.TeacherContractNotFound : teacherContract;
    }
}
