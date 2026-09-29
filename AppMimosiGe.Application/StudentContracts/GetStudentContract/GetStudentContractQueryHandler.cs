using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetStudentContractQueryHandler(IStudentContractsRepository repository)
    : IQueryHandler<GetStudentContractQuery, StudentContractResponse>
{
    public async Task<Result<StudentContractResponse>> Handle(GetStudentContractQuery request,
        CancellationToken cancellationToken)
    {
        StudentContractResponse? studentContract = await repository.GetOne(request.ScId, cancellationToken);
        return studentContract is null ? StudentContractErrors.StudentContractNotFound : studentContract;
    }
}
