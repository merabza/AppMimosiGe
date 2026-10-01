using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.GetBalancesFormLookups;
using AppMimosiGe.Application.Balances.GetStatement;
using AppMimosiGe.Application.Balances.GetStatementStudentContracts;
using AppMimosiGe.WebApi.DependencyInjection;
using AppMimosiGe.WebApi.Endpoints.V1;
using AppMimosiGeShared.Contracts.Errors;
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

public sealed class ChargesAndPaymentsEndpointsTests
{
    private static Mock<IQueryHandler<TQuery, TResponse>> QueryHandler<TQuery, TResponse>(Result<TResponse> result)
        where TQuery : IQuery<TResponse>
    {
        var handler = new Mock<IQueryHandler<TQuery, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    [Fact]
    public async Task GetRowsData_Success_ReturnsOkAndPassesTheRequest()
    {
        // Arrange
        var rows = new StatementRowsDataResponse(0, 0, 0m, 0m, []);
        Mock<IQueryHandler<GetStatementQuery, StatementRowsDataResponse>> handler =
            QueryHandler<GetStatementQuery, StatementRowsDataResponse>(rows);

        // Act
        Results<Ok<StatementRowsDataResponse>, ProblemHttpResult> result =
            await ChargesAndPaymentsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<StatementRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetStatementQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetStatementQuery, StatementRowsDataResponse>> handler =
            QueryHandler<GetStatementQuery, StatementRowsDataResponse>(
                Result.Failure<StatementRowsDataResponse>(BalanceErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<StatementRowsDataResponse>, ProblemHttpResult> result =
            await ChargesAndPaymentsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new BalancesFormLookupsResponse(11, []);
        Mock<IQueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse>> handler =
            QueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse>(lookups);

        // Act
        Results<Ok<BalancesFormLookupsResponse>, ProblemHttpResult> result =
            await ChargesAndPaymentsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<BalancesFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse>> handler =
            QueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse>(
                Result.Failure<BalancesFormLookupsResponse>(Error.NotFound("X", "x")));

        // Act
        Results<Ok<BalancesFormLookupsResponse>, ProblemHttpResult> result =
            await ChargesAndPaymentsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task GetStudentContracts_Success_ReturnsOkAndPassesTheYear()
    {
        // Arrange
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann 6.001")];
        Mock<IQueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>>> handler =
            QueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>>(contracts);

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await ChargesAndPaymentsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        Assert.Same(contracts, Assert.IsType<Ok<List<LookupItemResponse>>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetStatementStudentContractsQuery(11), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetStudentContracts_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>>> handler =
            QueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>>(
                Result.Failure<List<LookupItemResponse>>(Error.Problem("X", "x")));

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await ChargesAndPaymentsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/chargesandpayments/rowsdata", "GET")]
    [InlineData("api/v1/chargesandpayments/formlookups", "GET")]
    [InlineData("api/v1/chargesandpayments/studentcontracts", "GET")]
    public async Task UseAppMimosiGeApi_MapsChargesAndPaymentsEndpoints(string pattern, string method)
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseAppMimosiGeApi(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Contains(endpoints,
            e => e.RoutePattern.RawText?.TrimStart('/') == pattern &&
                 e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public async Task UseChargesAndPaymentsEndpoints_MapsThreeEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseChargesAndPaymentsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(3, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    // UseAppMimosiGeApi hands its debug logger on to the charges and payments endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheChargesAndPaymentsStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started",
                nameof(ChargesAndPaymentsEndpoints.UseChargesAndPaymentsEndpoints)), Times.Once);
        logger.Verify(
            l => l.Information("{MethodName} Finished",
                nameof(ChargesAndPaymentsEndpoints.UseChargesAndPaymentsEndpoints)), Times.Once);
    }
}
