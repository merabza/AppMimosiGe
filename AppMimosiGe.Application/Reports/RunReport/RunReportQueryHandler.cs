using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.RunReport;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RunReportQueryHandler(IServiceProvider services, IReportsRepository repository)
    : IQueryHandler<RunReportQuery, ReportResponse>
{
    //უცნობი გასაღები → 404, პარამეტრის შეცდომა → 400; შემდეგ რეპორტის საკუთარი query handler (ამავე DI scope-იდან).
    //რეპორტის უფლებას (AppClaim) endpoint-ის ფილტრი ამოწმებს (403)
    public async Task<Result<ReportResponse>> Handle(RunReportQuery query, CancellationToken cancellationToken)
    {
        ReportDefinition? definition = ReportsCatalog.Find(query.Key);
        if (definition is null)
        {
            return ReportErrors.ReportNotFound;
        }

        ReportParametersRequest parameters = ReportParameterRules.Normalize(query.Parameters);
        Error? error = ReportParameterRules.Validate(definition, parameters);
        if (error is not null)
        {
            return error;
        }

        Result<ReportTable> table = await definition.Run(parameters, services, cancellationToken);
        if (table.IsFailure)
        {
            return table.Error;
        }

        ReportLookupsResponse? lookups = ReportParameterRules.NeedsLookups(definition)
            ? await repository.GetLookups(cancellationToken)
            : null;
        return new ReportResponse(definition.Key, definition.Title, [
            .. definition.Parameters.Select(p => new ReportParameterValueResponse(p.Name, p.Caption,
                ReportParameterRules.DisplayValue(p.Name, parameters, lookups)))
        ], table.Value.Columns, table.Value.Sections, table.Value.FooterRows);
    }
}
