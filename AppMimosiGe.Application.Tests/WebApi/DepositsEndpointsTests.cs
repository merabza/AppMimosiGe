using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.GetBalancesFormLookups;
using AppMimosiGe.Application.Balances.GetDeposits;
using AppMimosiGe.Application.Balances.RecountBalances;
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

public sealed class DepositsEndpointsTests
{
    private static readonly DateTime DateTo = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Unspecified);

    private static Mock<IQueryHandler<TQuery, TResponse>> QueryHandler<TQuery, TResponse>(Result<TResponse> result)
        where TQuery : IQuery<TResponse>
    {
        var handler = new Mock<IQueryHandler<TQuery, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static Mock<ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>> RecountHandler(
        Result<BalancesRecountResponse> result)
    {
        var handler = new Mock<ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<RecountBalancesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return handler;
    }

    [Fact]
    public async Task GetRows_Success_ReturnsOkAndPassesTheParameters()
    {
        // Arrange
        var deposits = new DepositsResponse(0m, 0m, []);
        Mock<IQueryHandler<GetDepositsQuery, DepositsResponse>> handler =
            QueryHandler<GetDepositsQuery, DepositsResponse>(deposits);

        // Act
        Results<Ok<DepositsResponse>, ProblemHttpResult> result =
            await DepositsEndpoints.GetRows(DateTo, handler.Object, 11, -50.5m, "call");

        // Assert
        Assert.Same(deposits, Assert.IsType<Ok<DepositsResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetDepositsQuery(11, -50.5m, DateTo, "call"), It.IsAny<CancellationToken>()));
    }

    //without the optional parameters: every year, maximum 0, no filter
    [Fact]
    public async Task GetRows_Defaults_AreAllYearsMaximumZeroNoFilter()
    {
        // Arrange
        Mock<IQueryHandler<GetDepositsQuery, DepositsResponse>> handler =
            QueryHandler<GetDepositsQuery, DepositsResponse>(new DepositsResponse(0m, 0m, []));

        // Act
        await DepositsEndpoints.GetRows(DateTo, handler.Object);

        // Assert
        handler.Verify(h => h.Handle(new GetDepositsQuery(null, 0m, DateTo, null), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRows_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetDepositsQuery, DepositsResponse>> handler =
            QueryHandler<GetDepositsQuery, DepositsResponse>(
                Result.Failure<DepositsResponse>(BalanceErrors.DepositsFilterIsInvalid));

        // Act
        Results<Ok<DepositsResponse>, ProblemHttpResult> result =
            await DepositsEndpoints.GetRows(DateTo, handler.Object, filter: "x");

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
            await DepositsEndpoints.GetFormLookups(handler.Object);

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
            await DepositsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    //opening the page recounts only the dirty groups and contracts
    [Fact]
    public async Task Recount_RecountsTheDirtyOnes()
    {
        // Arrange
        var response = new BalancesRecountResponse(1, 1, 0, 2, 1);
        Mock<ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>> handler = RecountHandler(response);

        // Act
        Results<Ok<BalancesRecountResponse>, ProblemHttpResult> result =
            await DepositsEndpoints.Recount(handler.Object);

        // Assert
        Assert.Same(response, Assert.IsType<Ok<BalancesRecountResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new RecountBalancesCommand(true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullRecount_RecountsEverything()
    {
        // Arrange
        var response = new BalancesRecountResponse(51, 0, 0, 121, 0);
        Mock<ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>> handler = RecountHandler(response);

        // Act
        Results<Ok<BalancesRecountResponse>, ProblemHttpResult> result =
            await DepositsEndpoints.FullRecount(handler.Object);

        // Assert
        Assert.Same(response, Assert.IsType<Ok<BalancesRecountResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new RecountBalancesCommand(false), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recount_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>> handler =
            RecountHandler(Result.Failure<BalancesRecountResponse>(Error.Conflict("X", "x")));

        // Act
        Results<Ok<BalancesRecountResponse>, ProblemHttpResult> result =
            await DepositsEndpoints.Recount(handler.Object);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/deposits/rows", "GET")]
    [InlineData("api/v1/deposits/formlookups", "GET")]
    [InlineData("api/v1/deposits/recount", "POST")]
    [InlineData("api/v1/deposits/fullrecount", "POST")]
    public async Task UseAppMimosiGeApi_MapsDepositsEndpoints(string pattern, string method)
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
    public async Task UseDepositsEndpoints_MapsFourEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseDepositsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(4, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    // UseAppMimosiGeApi hands its debug logger on to the deposits endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheDepositsStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(DepositsEndpoints.UseDepositsEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(DepositsEndpoints.UseDepositsEndpoints)),
            Times.Once);
    }
}
