using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.RunReport;

//რეპორტი კატალოგის გასაღებით
public sealed record RunReportQuery(string Key, ReportParametersRequest Parameters) : IQuery<ReportResponse>;
