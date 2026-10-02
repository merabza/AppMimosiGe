using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.GetSalaryHeaders;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetSalaryHeadersQueryHandler(ISalaryRepository repository)
    : IQueryHandler<GetSalaryHeadersQuery, List<SalaryHeaderRowResponse>>
{
    //უწყისები წელიწადში თორმეტამდეა, ამიტომ სია გვერდებად არ იყოფა
    public async Task<Result<List<SalaryHeaderRowResponse>>> Handle(GetSalaryHeadersQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetHeaders(cancellationToken);
    }
}
