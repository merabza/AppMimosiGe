using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.GetSalaryFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetSalaryFormLookupsQueryHandler(ISalaryRepository repository)
    : IQueryHandler<GetSalaryFormLookupsQuery, SalaryFormLookupsResponse>
{
    public async Task<Result<SalaryFormLookupsResponse>> Handle(GetSalaryFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        return new SalaryFormLookupsResponse(await repository.GetEmployeeLookups(cancellationToken),
            await repository.GetPartTypeLookups(cancellationToken));
    }
}
