using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.CreateGroup;
using AppMimosiGe.Application.Groups.DeleteGroup;
using AppMimosiGe.Application.Groups.GetGroup;
using AppMimosiGe.Application.Groups.GetGroupFormLookups;
using AppMimosiGe.Application.Groups.GetGroupsRowsData;
using AppMimosiGe.Application.Groups.GetGroupStudentContracts;
using AppMimosiGe.Application.Groups.UpdateGroup;
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

public sealed class GroupsEndpointsTests
{
    private static readonly GroupResponse Group = new(7, 11, "1001", 6, 2, 10, null, true, [], [], []);

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
        var rows = new GroupsRowsDataResponse(0, 0, []);
        Mock<IQueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse>> handler =
            QueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse>(rows);

        // Act
        Results<Ok<GroupsRowsDataResponse>, ProblemHttpResult> result =
            await GroupsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<GroupsRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupsRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse>> handler =
            QueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse>(
                Result.Failure<GroupsRowsDataResponse>(GroupErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<GroupsRowsDataResponse>, ProblemHttpResult> result =
            await GroupsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new GroupFormLookupsResponse(11, [], [], [], [], [], [], [], [], []);
        Mock<IQueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse>> handler =
            QueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse>(lookups);

        // Act
        Results<Ok<GroupFormLookupsResponse>, ProblemHttpResult> result =
            await GroupsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<GroupFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse>> handler =
            QueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse>(
                Result.Failure<GroupFormLookupsResponse>(GroupErrors.CourseNotFound));

        // Act
        Results<Ok<GroupFormLookupsResponse>, ProblemHttpResult> result =
            await GroupsEndpoints.GetFormLookups(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetStudentContracts_Success_ReturnsOkAndPassesTheYear()
    {
        // Arrange
        List<GroupStudentContractLookupResponse> contracts = [new(20, "A B / 6.001", [])];
        Mock<IQueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>>> handler =
            QueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>>(contracts);

        // Act
        Results<Ok<List<GroupStudentContractLookupResponse>>, ProblemHttpResult> result =
            await GroupsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        Assert.Same(contracts, Assert.IsType<Ok<List<GroupStudentContractLookupResponse>>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupStudentContractsQuery(11), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetStudentContracts_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>>> handler =
            QueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>>(
                Result.Failure<List<GroupStudentContractLookupResponse>>(GroupErrors.AcademicYearNotFound));

        // Act
        Results<Ok<List<GroupStudentContractLookupResponse>>, ProblemHttpResult> result =
            await GroupsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupQuery, GroupResponse>> handler = QueryHandler<GetGroupQuery, GroupResponse>(Group);

        // Act
        Results<Ok<GroupResponse>, ProblemHttpResult> result = await GroupsEndpoints.GetOne(7, handler.Object);

        // Assert
        Assert.Same(Group, Assert.IsType<Ok<GroupResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupQuery(7), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupQuery, GroupResponse>> handler =
            QueryHandler<GetGroupQuery, GroupResponse>(Result.Failure<GroupResponse>(GroupErrors.GroupNotFound));

        // Act
        Results<Ok<GroupResponse>, ProblemHttpResult> result = await GroupsEndpoints.GetOne(7, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new GroupRequest();
        var handler = new Mock<ICommandHandler<CreateGroupCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(52));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await GroupsEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(52, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateGroupCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_Conflict_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<CreateGroupCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(GroupErrors.GroupCodeAlreadyExists));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await GroupsEndpoints.Create(new GroupRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new GroupRequest();
        var handler = new Mock<ICommandHandler<UpdateGroupCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupsEndpoints.Update(42, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateGroupCommand(42, request), It.IsAny<CancellationToken>()));
    }

    // a student row with lessons can not be removed from the group
    [Fact]
    public async Task Update_StudentInUse_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<UpdateGroupCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(GroupErrors.GroupStudentIsInUse));

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupsEndpoints.Update(42, new GroupRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<DeleteGroupCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<DeleteGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupsEndpoints.Delete(42, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteGroupCommand(42), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_InUse_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<DeleteGroupCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<DeleteGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(GroupErrors.GroupIsInUse));

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupsEndpoints.Delete(42, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/groups/rowsdata", "GET")]
    [InlineData("api/v1/groups/formlookups", "GET")]
    [InlineData("api/v1/groups/studentcontracts", "GET")]
    [InlineData("api/v1/groups/{grpId:int}", "GET")]
    [InlineData("api/v1/groups/", "POST")]
    [InlineData("api/v1/groups/{grpId:int}", "PUT")]
    [InlineData("api/v1/groups/{grpId:int}", "DELETE")]
    public async Task UseAppMimosiGeApi_MapsGroupsEndpoints(string pattern, string method)
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
    public async Task UseGroupsEndpoints_MapsSevenEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseGroupsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(7, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseGroupsEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseGroupsEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(GroupsEndpoints.UseGroupsEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(GroupsEndpoints.UseGroupsEndpoints)),
            Times.Once);
    }
}
