using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.GetReportLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetReportLookupsQueryHandler(IReportsRepository repository)
    : IQueryHandler<GetReportLookupsQuery, ReportLookupsResponse>
{
    public async Task<Result<ReportLookupsResponse>> Handle(GetReportLookupsQuery query,
        CancellationToken cancellationToken)
    {
        return await repository.GetLookups(cancellationToken);
    }
}
