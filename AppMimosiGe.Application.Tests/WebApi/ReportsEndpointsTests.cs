using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.GetReportCatalog;
using AppMimosiGe.Application.Reports.GetReportExcel;
using AppMimosiGe.Application.Reports.GetReportLookups;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.RunReport;
using AppMimosiGe.WebApi.DependencyInjection;
using AppMimosiGe.WebApi.Endpoints.V1;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Moq;
using Serilog;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.WebApi;

public sealed class ReportsEndpointsTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime End = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly ReportResponse Report = new("r03RoomsAgenda", "ოთახების ცხრილი", [], [], [], []);

    private static Mock<IQueryHandler<TQuery, TResponse>> QueryHandler<TQuery, TResponse>(Result<TResponse> result)
        where TQuery : IQuery<TResponse>
    {
        var handler = new Mock<IQueryHandler<TQuery, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static void AssertProblem(IResult result, int statusCode)
    {
        Assert.Equal(statusCode, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetCatalog_Success_ReturnsOk()
    {
        // Arrange
        var catalog = new ReportCatalogResponse([], []);

        // Act
        Results<Ok<ReportCatalogResponse>, ProblemHttpResult> result = await ReportsEndpoints.GetCatalog(
            QueryHandler<GetReportCatalogQuery, ReportCatalogResponse>(catalog).Object);

        // Assert
        Assert.Same(catalog, Assert.IsType<Ok<ReportCatalogResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetCatalog_Failure_ReturnsProblem()
    {
        // Act
        Results<Ok<ReportCatalogResponse>, ProblemHttpResult> result = await ReportsEndpoints.GetCatalog(
            QueryHandler<GetReportCatalogQuery, ReportCatalogResponse>(Error.Problem("X", "x")).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new ReportLookupsResponse([], [], []);

        // Act
        Results<Ok<ReportLookupsResponse>, ProblemHttpResult> result = await ReportsEndpoints.GetLookups(
            QueryHandler<GetReportLookupsQuery, ReportLookupsResponse>(lookups).Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<ReportLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetLookups_Failure_ReturnsProblem()
    {
        // Act
        Results<Ok<ReportLookupsResponse>, ProblemHttpResult> result = await ReportsEndpoints.GetLookups(
            QueryHandler<GetReportLookupsQuery, ReportLookupsResponse>(Error.Problem("X", "x")).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    // the key and every parameter of the link reach the handler
    [Fact]
    public async Task Run_PassesTheKeyAndParametersAndReturnsOk()
    {
        // Arrange
        Mock<IQueryHandler<RunReportQuery, ReportResponse>> handler =
            QueryHandler<RunReportQuery, ReportResponse>(Report);

        // Act
        Results<Ok<ReportResponse>, ProblemHttpResult> result =
            await ReportsEndpoints.Run("r03RoomsAgenda", Start, End, 5, 3, 20, handler.Object);

        // Assert
        Assert.Same(Report, Assert.IsType<Ok<ReportResponse>>(result.Result).Value);
        handler.Verify(
            h => h.Handle(new RunReportQuery("r03RoomsAgenda", new ReportParametersRequest(Start, End, 5, 3, 20)),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_UnknownReport_Returns404()
    {
        // Act
        Results<Ok<ReportResponse>, ProblemHttpResult> result = await ReportsEndpoints.Run("rX", null, End, null, null,
            null, QueryHandler<RunReportQuery, ReportResponse>(ReportErrors.ReportNotFound).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Run_MissingParameter_Returns400()
    {
        // Act
        Results<Ok<ReportResponse>, ProblemHttpResult> result = await ReportsEndpoints.Run("r03RoomsAgenda", null, null,
            null, null, null,
            QueryHandler<RunReportQuery, ReportResponse>(ReportErrors.ParameterIsRequired("თარიღისთვის")).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    // the xlsx file with its name
    [Fact]
    public async Task Excel_Success_ReturnsTheFile()
    {
        // Arrange
        var file = new ReportFile([1, 2, 3], "r03RoomsAgenda_2026-10-02.xlsx");
        Mock<IQueryHandler<GetReportExcelQuery, ReportFile>> handler =
            QueryHandler<GetReportExcelQuery, ReportFile>(file);

        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result =
            await ReportsEndpoints.Excel("r03RoomsAgenda", Start, End, 5, 3, 20, handler.Object);

        // Assert
        var fileResult = Assert.IsType<FileContentHttpResult>(result.Result);
        Assert.Equal(file.Content, fileResult.FileContents.ToArray());
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("r03RoomsAgenda_2026-10-02.xlsx", fileResult.FileDownloadName);
        handler.Verify(
            h => h.Handle(new GetReportExcelQuery("r03RoomsAgenda", new ReportParametersRequest(Start, End, 5, 3, 20)),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Excel_Failure_ReturnsProblem()
    {
        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result = await ReportsEndpoints.Excel("rX", null, End, null,
            null, null, QueryHandler<GetReportExcelQuery, ReportFile>(ReportErrors.ReportNotFound).Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("api/v1/reports/catalog")]
    [InlineData("api/v1/reports/lookups")]
    [InlineData("api/v1/reports/{key}")]
    [InlineData("api/v1/reports/{key}/excel")]
    public async Task UseAppMimosiGeApi_MapsReportsEndpoints(string pattern)
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseAppMimosiGeApi(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Contains(endpoints,
            e => e.RoutePattern.RawText?.TrimStart('/') == pattern &&
                 e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public async Task UseReportsEndpoints_MapsFourEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseReportsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(4, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseReportsEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseReportsEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(ReportsEndpoints.UseReportsEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(ReportsEndpoints.UseReportsEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the reports endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheReportsStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(ReportsEndpoints.UseReportsEndpoints)),
            Times.Once);
    }
}
