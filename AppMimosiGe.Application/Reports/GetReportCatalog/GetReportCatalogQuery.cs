using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.GetReportCatalog;

public sealed record GetReportCatalogQuery : IQuery<ReportCatalogResponse>;
