using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.GetReportCatalog;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetReportCatalogQueryHandler(IUserClaimRights claimRights)
    : IQueryHandler<GetReportCatalogQuery, ReportCatalogResponse>
{
    //მხოლოდ ის რეპორტები, რომელთა AppClaim მომხმარებელს აქვს (D112)
    public async Task<Result<ReportCatalogResponse>> Handle(GetReportCatalogQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<string> claims = await claimRights.GetClaims(cancellationToken);
        return ReportsCatalog.ForClaims(claims);
    }
}
