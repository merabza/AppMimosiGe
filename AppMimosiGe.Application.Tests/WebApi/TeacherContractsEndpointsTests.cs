using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;
using AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;
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
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.WebApi;

public sealed class TeacherContractsEndpointsTests
{
    private static readonly TeacherContractResponse Contract = new(7, "T3.07",
        new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), 1, "T", null, null, false, false, null, 2, 0,
        false, null, null, null, null, null, null);

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
    public async Task GetRowsData_Success_ReturnsOkAndPassesTheRequest()
    {
        // Arrange
        var rows = new TeacherContractsRowsDataResponse(0, 0, []);
        Mock<IQueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse>> handler =
            QueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse>(rows);

        // Act
        Results<Ok<TeacherContractsRowsDataResponse>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<TeacherContractsRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetTeacherContractsRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse>> handler =
            QueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse>(
                Result.Failure<TeacherContractsRowsDataResponse>(TeacherContractErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<TeacherContractsRowsDataResponse>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new TeacherContractFormLookupsResponse([], [], [], []);
        Mock<IQueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse>> handler =
            QueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse>(lookups);

        // Act
        Results<Ok<TeacherContractFormLookupsResponse>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<TeacherContractFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse>> handler =
            QueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse>(
                Result.Failure<TeacherContractFormLookupsResponse>(TeacherContractErrors.TeacherNotFound));

        // Act
        Results<Ok<TeacherContractFormLookupsResponse>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.GetFormLookups(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SearchHumans_Success_ReturnsOkAndPassesTheSearch()
    {
        // Arrange
        List<LookupItemResponse> humans = [new(1, "A B")];
        Mock<IQueryHandler<SearchHumansQuery, List<LookupItemResponse>>> handler =
            QueryHandler<SearchHumansQuery, List<LookupItemResponse>>(humans);

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.SearchHumans("ab", handler.Object);

        // Assert
        Assert.Same(humans, Assert.IsType<Ok<List<LookupItemResponse>>>(result.Result).Value);
        handler.Verify(h => h.Handle(new SearchHumansQuery("ab"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SearchHumans_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<SearchHumansQuery, List<LookupItemResponse>>> handler =
            QueryHandler<SearchHumansQuery, List<LookupItemResponse>>(
                Result.Failure<List<LookupItemResponse>>(TeacherContractErrors.TeacherNotFound));

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.SearchHumans("ab", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetTeacherContractQuery, TeacherContractResponse>> handler =
            QueryHandler<GetTeacherContractQuery, TeacherContractResponse>(Contract);

        // Act
        Results<Ok<TeacherContractResponse>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.GetOne(7, handler.Object);

        // Assert
        Assert.Same(Contract, Assert.IsType<Ok<TeacherContractResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetTeacherContractQuery(7), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetTeacherContractQuery, TeacherContractResponse>> handler =
            QueryHandler<GetTeacherContractQuery, TeacherContractResponse>(
                Result.Failure<TeacherContractResponse>(TeacherContractErrors.TeacherContractNotFound));

        // Act
        Results<Ok<TeacherContractResponse>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.GetOne(7, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new TeacherContractRequest();
        var handler = new Mock<ICommandHandler<CreateTeacherContractCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateTeacherContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(31));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await TeacherContractsEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(31, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateTeacherContractCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_Conflict_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<CreateTeacherContractCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateTeacherContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(TeacherContractErrors.ContractNumberAlreadyExists));

        // Act
        Results<Ok<int>, ProblemHttpResult> result =
            await TeacherContractsEndpoints.Create(new TeacherContractRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new TeacherContractRequest();
        var handler = new Mock<ICommandHandler<UpdateTeacherContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateTeacherContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await TeacherContractsEndpoints.Update(42, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateTeacherContractCommand(42, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_NotFound_Returns404Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<UpdateTeacherContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateTeacherContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(TeacherContractErrors.TeacherContractNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result =
            await TeacherContractsEndpoints.Update(42, new TeacherContractRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<DeleteTeacherContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<DeleteTeacherContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await TeacherContractsEndpoints.Delete(42, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteTeacherContractCommand(42), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_InUse_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<DeleteTeacherContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<DeleteTeacherContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(TeacherContractErrors.TeacherContractIsInUse));

        // Act
        Results<Ok, ProblemHttpResult> result = await TeacherContractsEndpoints.Delete(42, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/teachercontracts/rowsdata", "GET")]
    [InlineData("api/v1/teachercontracts/formlookups", "GET")]
    [InlineData("api/v1/teachercontracts/humans", "GET")]
    [InlineData("api/v1/teachercontracts/{id:int}", "GET")]
    [InlineData("api/v1/teachercontracts/", "POST")]
    [InlineData("api/v1/teachercontracts/{id:int}", "PUT")]
    [InlineData("api/v1/teachercontracts/{id:int}", "DELETE")]
    public async Task UseAppMimosiGeApi_MapsTeacherContractsEndpoints(string pattern, string method)
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
    public async Task UseTeacherContractsEndpoints_MapsSevenEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseTeacherContractsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(7, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }
}
