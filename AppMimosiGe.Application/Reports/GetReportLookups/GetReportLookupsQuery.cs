using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.GetReportLookups;

public sealed record GetReportLookupsQuery : IQuery<ReportLookupsResponse>;
