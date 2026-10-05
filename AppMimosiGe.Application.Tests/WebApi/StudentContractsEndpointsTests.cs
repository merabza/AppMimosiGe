using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.CreateStudentContract;
using AppMimosiGe.Application.StudentContracts.DeleteStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;
using AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.StudentContracts.UpdateStudentContract;
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

public sealed class StudentContractsEndpointsTests
{
    private static readonly StudentContractResponse Contract = new(7, "6.007",
        new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), 1, "S", 2, "P", 11, null, null, null, true, []);

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
        var rows = new StudentContractsRowsDataResponse(0, 0, []);
        Mock<IQueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse>> handler =
            QueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse>(rows);

        // Act
        Results<Ok<StudentContractsRowsDataResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<StudentContractsRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetStudentContractsRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse>> handler =
            QueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse>(
                Result.Failure<StudentContractsRowsDataResponse>(StudentContractErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<StudentContractsRowsDataResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new StudentContractFormLookupsResponse(11, [], [], [], []);
        Mock<IQueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse>> handler =
            QueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse>(lookups);

        // Act
        Results<Ok<StudentContractFormLookupsResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<StudentContractFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse>> handler =
            QueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse>(
                Result.Failure<StudentContractFormLookupsResponse>(StudentContractErrors.AcademicYearNotFound));

        // Act
        Results<Ok<StudentContractFormLookupsResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetFormLookups(handler.Object);

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
            await StudentContractsEndpoints.SearchHumans("ab", handler.Object);

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
                Result.Failure<List<LookupItemResponse>>(StudentContractErrors.StudentNotFound));

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await StudentContractsEndpoints.SearchHumans(null, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetStudentContractQuery, StudentContractResponse>> handler =
            QueryHandler<GetStudentContractQuery, StudentContractResponse>(Contract);

        // Act
        Results<Ok<StudentContractResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetOne(7, handler.Object);

        // Assert
        Assert.Same(Contract, Assert.IsType<Ok<StudentContractResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetStudentContractQuery(7), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetStudentContractQuery, StudentContractResponse>> handler =
            QueryHandler<GetStudentContractQuery, StudentContractResponse>(
                Result.Failure<StudentContractResponse>(StudentContractErrors.StudentContractNotFound));

        // Act
        Results<Ok<StudentContractResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetOne(7, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new StudentContractRequest();
        var handler = new Mock<ICommandHandler<CreateStudentContractCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateStudentContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(128));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await StudentContractsEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(128, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateStudentContractCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_Conflict_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<CreateStudentContractCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateStudentContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(StudentContractErrors.ContractNumberAlreadyExists));

        // Act
        Results<Ok<int>, ProblemHttpResult> result =
            await StudentContractsEndpoints.Create(new StudentContractRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new StudentContractRequest();
        var handler = new Mock<ICommandHandler<UpdateStudentContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateStudentContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await StudentContractsEndpoints.Update(42, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateStudentContractCommand(42, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_Failure_ReturnsProblem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<UpdateStudentContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateStudentContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(StudentContractErrors.DetailNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result =
            await StudentContractsEndpoints.Update(42, new StudentContractRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<DeleteStudentContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<DeleteStudentContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await StudentContractsEndpoints.Delete(42, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteStudentContractCommand(42), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_InUse_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<DeleteStudentContractCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<DeleteStudentContractCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(StudentContractErrors.StudentContractIsInUse));

        // Act
        Results<Ok, ProblemHttpResult> result = await StudentContractsEndpoints.Delete(42, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/studentcontracts/rowsdata", "GET")]
    [InlineData("api/v1/studentcontracts/formlookups", "GET")]
    [InlineData("api/v1/studentcontracts/humans", "GET")]
    [InlineData("api/v1/studentcontracts/{scId:int}", "GET")]
    [InlineData("api/v1/studentcontracts/", "POST")]
    [InlineData("api/v1/studentcontracts/{scId:int}", "PUT")]
    [InlineData("api/v1/studentcontracts/{scId:int}", "DELETE")]
    public async Task UseAppMimosiGeApi_MapsStudentContractsEndpoints(string pattern, string method)
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
    public async Task UseStudentContractsEndpoints_MapsEightEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseStudentContractsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(8, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsEachStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        foreach (string methodName in new[]
                 {
                     nameof(AppMimosiGeApiDependencyInjection.UseAppMimosiGeApi),
                     nameof(StudentContractsEndpoints.UseStudentContractsEndpoints),
                     nameof(TeacherContractsEndpoints.UseTeacherContractsEndpoints)
                 })
        {
            logger.Verify(l => l.Information("{MethodName} Started", methodName), Times.Once);
            logger.Verify(l => l.Information("{MethodName} Finished", methodName), Times.Once);
        }
    }
}
