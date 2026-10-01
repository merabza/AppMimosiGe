using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.CreateCrmCall;
using AppMimosiGe.Application.CrmCalls.DeleteCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCallFormLookups;
using AppMimosiGe.Application.CrmCalls.GetCrmCallsRowsData;
using AppMimosiGe.Application.CrmCalls.GetCrmCallStudentContracts;
using AppMimosiGe.Application.CrmCalls.UpdateCrmCall;
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

public sealed class CrmCallsEndpointsTests
{
    private static readonly CrmCallResponse CrmCall = new(5, 10, "Alpha Ann / 6.001", 11, 1,
        new DateTime(2026, 9, 24, 19, 48, 0, DateTimeKind.Unspecified), 3, "text", null);

    private static Mock<IQueryHandler<TQuery, TResponse>> QueryHandler<TQuery, TResponse>(Result<TResponse> result)
        where TQuery : IQuery<TResponse>
    {
        var handler = new Mock<IQueryHandler<TQuery, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static Mock<ICommandHandler<TCommand>> CommandHandler<TCommand>(Result result) where TCommand : ICommand
    {
        var handler = new Mock<ICommandHandler<TCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static void AssertProblem(IResult result, int statusCode)
    {
        Assert.Equal(statusCode, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetRowsData_Success_ReturnsOkAndPassesTheRequest()
    {
        // Arrange
        var rows = new CrmCallsRowsDataResponse(0, 0, []);
        Mock<IQueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse>> handler =
            QueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse>(rows);

        // Act
        Results<Ok<CrmCallsRowsDataResponse>, ProblemHttpResult> result =
            await CrmCallsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<CrmCallsRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetCrmCallsRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse>> handler =
            QueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse>(
                Result.Failure<CrmCallsRowsDataResponse>(CrmCallErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<CrmCallsRowsDataResponse>, ProblemHttpResult> result =
            await CrmCallsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new CrmCallFormLookupsResponse(11, [], [], []);
        Mock<IQueryHandler<GetCrmCallFormLookupsQuery, CrmCallFormLookupsResponse>> handler =
            QueryHandler<GetCrmCallFormLookupsQuery, CrmCallFormLookupsResponse>(lookups);

        // Act
        Results<Ok<CrmCallFormLookupsResponse>, ProblemHttpResult> result =
            await CrmCallsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<CrmCallFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetCrmCallFormLookupsQuery, CrmCallFormLookupsResponse>> handler =
            QueryHandler<GetCrmCallFormLookupsQuery, CrmCallFormLookupsResponse>(
                Result.Failure<CrmCallFormLookupsResponse>(CrmCallErrors.CrmCallNotFound));

        // Act
        Results<Ok<CrmCallFormLookupsResponse>, ProblemHttpResult> result =
            await CrmCallsEndpoints.GetFormLookups(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetStudentContracts_Success_ReturnsOkAndPassesTheYear()
    {
        // Arrange
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann / 6.001")];
        Mock<IQueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>>> handler =
            QueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>>(contracts);

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await CrmCallsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        Assert.Same(contracts, Assert.IsType<Ok<List<LookupItemResponse>>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetCrmCallStudentContractsQuery(11), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetStudentContracts_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>>> handler =
            QueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>>(
                Result.Failure<List<LookupItemResponse>>(CrmCallErrors.StudentContractNotFound));

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await CrmCallsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetCrmCallQuery, CrmCallResponse>> handler =
            QueryHandler<GetCrmCallQuery, CrmCallResponse>(CrmCall);

        // Act
        Results<Ok<CrmCallResponse>, ProblemHttpResult> result = await CrmCallsEndpoints.GetOne(5, handler.Object);

        // Assert
        Assert.Same(CrmCall, Assert.IsType<Ok<CrmCallResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetCrmCallQuery(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetCrmCallQuery, CrmCallResponse>> handler =
            QueryHandler<GetCrmCallQuery, CrmCallResponse>(
                Result.Failure<CrmCallResponse>(CrmCallErrors.CrmCallNotFound));

        // Act
        Results<Ok<CrmCallResponse>, ProblemHttpResult> result = await CrmCallsEndpoints.GetOne(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsTheNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new CrmCallRequest { StudentContractId = 10 };
        var handler = new Mock<ICommandHandler<CreateCrmCallCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateCrmCallCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(77));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await CrmCallsEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(77, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateCrmCallCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<CreateCrmCallCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateCrmCallCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(CrmCallErrors.AnswerTypeIsRequired));

        // Act
        Results<Ok<int>, ProblemHttpResult> result =
            await CrmCallsEndpoints.Create(new CrmCallRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new CrmCallRequest { StudentContractId = 10 };
        Mock<ICommandHandler<UpdateCrmCallCommand>> handler = CommandHandler<UpdateCrmCallCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await CrmCallsEndpoints.Update(5, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateCrmCallCommand(5, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_MissingCall_Returns404Problem()
    {
        // Arrange
        Mock<ICommandHandler<UpdateCrmCallCommand>> handler =
            CommandHandler<UpdateCrmCallCommand>(Result.Failure(CrmCallErrors.CrmCallNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result = await CrmCallsEndpoints.Update(5, new CrmCallRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<ICommandHandler<DeleteCrmCallCommand>> handler = CommandHandler<DeleteCrmCallCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await CrmCallsEndpoints.Delete(5, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteCrmCallCommand(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_MissingCall_Returns404Problem()
    {
        // Arrange
        Mock<ICommandHandler<DeleteCrmCallCommand>> handler =
            CommandHandler<DeleteCrmCallCommand>(Result.Failure(CrmCallErrors.CrmCallNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result = await CrmCallsEndpoints.Delete(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/crmcalls/rowsdata", "GET")]
    [InlineData("api/v1/crmcalls/formlookups", "GET")]
    [InlineData("api/v1/crmcalls/studentcontracts", "GET")]
    [InlineData("api/v1/crmcalls/{crmCallId:int}", "GET")]
    [InlineData("api/v1/crmcalls/", "POST")]
    [InlineData("api/v1/crmcalls/{crmCallId:int}", "PUT")]
    [InlineData("api/v1/crmcalls/{crmCallId:int}", "DELETE")]
    public async Task UseAppMimosiGeApi_MapsCrmCallsEndpoints(string pattern, string method)
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
    public async Task UseCrmCallsEndpoints_MapsSevenEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseCrmCallsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(7, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseCrmCallsEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseCrmCallsEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(CrmCallsEndpoints.UseCrmCallsEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(CrmCallsEndpoints.UseCrmCallsEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the CRM calls endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheCrmCallsStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(CrmCallsEndpoints.UseCrmCallsEndpoints)),
            Times.Once);
    }
}
