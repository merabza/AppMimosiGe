using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.GetReportCatalog;
using AppMimosiGe.Application.Reports.GetReportExcel;
using AppMimosiGe.Application.Reports.GetReportLookups;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.RunReport;
using AppMimosiGe.WebApi.Filters;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Serilog;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using WebSystemTools.WebApi.Abstractions.Infrastructure;
using Routes = AppMimosiGeShared.Contracts.V1.Routes.AppMimosiGeApiRoutes;

namespace AppMimosiGe.WebApi.Endpoints.V1;

//რეპორტები (Access-ის FrmMain): კატალოგი, ფილტრების სიები, რეპორტი გასაღებით და მისი Excel-ის ფაილი
public static class ReportsEndpoints
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static bool UseReportsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseReportsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.ReportsRoute.ReportsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveReportsRightsFilter>();

        group.MapGet(Routes.ReportsRoute.Catalog, GetCatalog);
        group.MapGet(Routes.ReportsRoute.Lookups, GetLookups);
        group.MapGet(Routes.ReportsRoute.Run, Run).AddEndpointFilter<UserMustHaveReportRightFilter>();
        group.MapGet(Routes.ReportsRoute.Excel, Excel).AddEndpointFilter<UserMustHaveReportRightFilter>();

        debugLogger?.Information("{MethodName} Finished", nameof(UseReportsEndpoints));
        return true;
    }

    // GET api/v1/reports/catalog
    internal static async Task<Results<Ok<ReportCatalogResponse>, ProblemHttpResult>> GetCatalog(
        IQueryHandler<GetReportCatalogQuery, ReportCatalogResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<ReportCatalogResponse> result = await handler.Handle(new GetReportCatalogQuery(), cancellationToken);
        return result.Match<ReportCatalogResponse, Results<Ok<ReportCatalogResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/reports/lookups
    internal static async Task<Results<Ok<ReportLookupsResponse>, ProblemHttpResult>> GetLookups(
        IQueryHandler<GetReportLookupsQuery, ReportLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<ReportLookupsResponse> result = await handler.Handle(new GetReportLookupsQuery(), cancellationToken);
        return result.Match<ReportLookupsResponse, Results<Ok<ReportLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/reports/{key}?startDate=&endDate=&teacherId=&courseId=&studentId=&academicYearId=
    internal static async Task<Results<Ok<ReportResponse>, ProblemHttpResult>> Run([FromRoute] string key,
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? teacherId,
        [FromQuery] int? courseId, [FromQuery] int? studentId, IQueryHandler<RunReportQuery, ReportResponse> handler,
        [FromQuery] int? academicYearId = null, CancellationToken cancellationToken = default)
    {
        Result<ReportResponse> result = await handler.Handle(
            new RunReportQuery(key,
                new ReportParametersRequest(startDate, endDate, teacherId, courseId, studentId, academicYearId)),
            cancellationToken);
        return result.Match<ReportResponse, Results<Ok<ReportResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/reports/{key}/excel?startDate=&endDate=&teacherId=&courseId=&studentId=&academicYearId=
    internal static async Task<Results<FileContentHttpResult, ProblemHttpResult>> Excel([FromRoute] string key,
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int? teacherId,
        [FromQuery] int? courseId, [FromQuery] int? studentId, IQueryHandler<GetReportExcelQuery, ReportFile> handler,
        [FromQuery] int? academicYearId = null, CancellationToken cancellationToken = default)
    {
        Result<ReportFile> result = await handler.Handle(
            new GetReportExcelQuery(key,
                new ReportParametersRequest(startDate, endDate, teacherId, courseId, studentId, academicYearId)),
            cancellationToken);
        return result.Match<ReportFile, Results<FileContentHttpResult, ProblemHttpResult>>(
            success => TypedResults.File(success.Content, ExcelContentType, success.FileName),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
