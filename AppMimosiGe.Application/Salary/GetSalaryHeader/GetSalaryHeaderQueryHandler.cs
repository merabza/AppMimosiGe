using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.GetSalaryHeader;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetSalaryHeaderQueryHandler(ISalaryRepository repository)
    : IQueryHandler<GetSalaryHeaderQuery, SalaryHeaderResponse>
{
    public async Task<Result<SalaryHeaderResponse>> Handle(GetSalaryHeaderQuery request,
        CancellationToken cancellationToken)
    {
        SalaryHeaderResponse? header = await repository.GetHeader(request.ShId, cancellationToken);
        return header is null ? SalaryErrors.SalaryHeaderNotFound : header;
    }
}
