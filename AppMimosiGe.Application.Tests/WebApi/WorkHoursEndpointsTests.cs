using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;
using AppMimosiGe.Application.WorkHours.CreateWorkHour;
using AppMimosiGe.Application.WorkHours.DeleteWorkHour;
using AppMimosiGe.Application.WorkHours.EndWork;
using AppMimosiGe.Application.WorkHours.GetWorkHour;
using AppMimosiGe.Application.WorkHours.GetWorkHourFormLookups;
using AppMimosiGe.Application.WorkHours.GetWorkHoursRowsData;
using AppMimosiGe.Application.WorkHours.StartWork;
using AppMimosiGe.Application.WorkHours.UpdateWorkHour;
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

public sealed class WorkHoursEndpointsTests
{
    private static readonly WorkHourResponse WorkHour = new(5, 15, "Alpha Ann / T3.10",
        new DateTime(2026, 10, 1, 9, 55, 12, DateTimeKind.Unspecified), null);

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

    private static Mock<ICommandHandler<TCommand, TResponse>> CommandHandler<TCommand, TResponse>(
        Result<TResponse> result) where TCommand : ICommand<TResponse>
    {
        var handler = new Mock<ICommandHandler<TCommand, TResponse>>();
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
        var rows = new WorkHoursRowsDataResponse(0, 0, [], []);
        Mock<IQueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse>> handler =
            QueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse>(rows);

        // Act
        Results<Ok<WorkHoursRowsDataResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<WorkHoursRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetWorkHoursRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse>> handler =
            QueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse>(
                Result.Failure<WorkHoursRowsDataResponse>(WorkHourErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<WorkHoursRowsDataResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new WorkHourFormLookupsResponse([new LookupItemResponse(15, "Alpha Ann / T3.10")]);
        Mock<IQueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse>> handler =
            QueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse>(lookups);

        // Act
        Results<Ok<WorkHourFormLookupsResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<WorkHourFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse>> handler =
            QueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse>(
                Result.Failure<WorkHourFormLookupsResponse>(WorkHourErrors.WorkHourNotFound));

        // Act
        Results<Ok<WorkHourFormLookupsResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.GetFormLookups(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetWorkHourQuery, WorkHourResponse>> handler =
            QueryHandler<GetWorkHourQuery, WorkHourResponse>(WorkHour);

        // Act
        Results<Ok<WorkHourResponse>, ProblemHttpResult> result = await WorkHoursEndpoints.GetOne(5, handler.Object);

        // Assert
        Assert.Same(WorkHour, Assert.IsType<Ok<WorkHourResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetWorkHourQuery(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetWorkHourQuery, WorkHourResponse>> handler =
            QueryHandler<GetWorkHourQuery, WorkHourResponse>(
                Result.Failure<WorkHourResponse>(WorkHourErrors.WorkHourNotFound));

        // Act
        Results<Ok<WorkHourResponse>, ProblemHttpResult> result = await WorkHoursEndpoints.GetOne(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsTheNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new WorkHourRequest { TeacherContractId = 15 };
        Mock<ICommandHandler<CreateWorkHourCommand, int>> handler =
            CommandHandler<CreateWorkHourCommand, int>(Result.Success(77));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await WorkHoursEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(77, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateWorkHourCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Create_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<ICommandHandler<CreateWorkHourCommand, int>> handler =
            CommandHandler<CreateWorkHourCommand, int>(Result.Failure<int>(WorkHourErrors.EndMustBeAfterStart));

        // Act
        Results<Ok<int>, ProblemHttpResult> result =
            await WorkHoursEndpoints.Create(new WorkHourRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new WorkHourRequest { TeacherContractId = 15 };
        Mock<ICommandHandler<UpdateWorkHourCommand>> handler = CommandHandler<UpdateWorkHourCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await WorkHoursEndpoints.Update(5, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateWorkHourCommand(5, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_MissingRecord_Returns404Problem()
    {
        // Arrange
        Mock<ICommandHandler<UpdateWorkHourCommand>> handler =
            CommandHandler<UpdateWorkHourCommand>(Result.Failure(WorkHourErrors.WorkHourNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result =
            await WorkHoursEndpoints.Update(5, new WorkHourRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<ICommandHandler<DeleteWorkHourCommand>> handler = CommandHandler<DeleteWorkHourCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await WorkHoursEndpoints.Delete(5, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteWorkHourCommand(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_MissingRecord_Returns404Problem()
    {
        // Arrange
        Mock<ICommandHandler<DeleteWorkHourCommand>> handler =
            CommandHandler<DeleteWorkHourCommand>(Result.Failure(WorkHourErrors.WorkHourNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result = await WorkHoursEndpoints.Delete(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Start_Success_ReturnsTheRecordAndPassesTheRequest()
    {
        // Arrange
        var request = new WorkTimeFixRequest { TeacherContractId = 15, LuftMinutes = 5 };
        Mock<ICommandHandler<StartWorkCommand, WorkHourResponse>> handler =
            CommandHandler<StartWorkCommand, WorkHourResponse>(WorkHour);

        // Act
        Results<Ok<WorkHourResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.Start(request, handler.Object);

        // Assert
        Assert.Same(WorkHour, Assert.IsType<Ok<WorkHourResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new StartWorkCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Start_RecordOfToday_Returns409Problem()
    {
        // Arrange
        Mock<ICommandHandler<StartWorkCommand, WorkHourResponse>> handler =
            CommandHandler<StartWorkCommand, WorkHourResponse>(
                Result.Failure<WorkHourResponse>(WorkHourErrors.TodayRecordExists));

        // Act
        Results<Ok<WorkHourResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.Start(new WorkTimeFixRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task End_Success_ReturnsTheRecordAndPassesTheRequest()
    {
        // Arrange
        var request = new WorkTimeFixRequest { TeacherContractId = 15, LuftMinutes = 0 };
        Mock<ICommandHandler<EndWorkCommand, WorkHourResponse>> handler =
            CommandHandler<EndWorkCommand, WorkHourResponse>(WorkHour);

        // Act
        Results<Ok<WorkHourResponse>, ProblemHttpResult> result = await WorkHoursEndpoints.End(request, handler.Object);

        // Assert
        Assert.Same(WorkHour, Assert.IsType<Ok<WorkHourResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new EndWorkCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task End_LuftTooBig_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<ICommandHandler<EndWorkCommand, WorkHourResponse>> handler =
            CommandHandler<EndWorkCommand, WorkHourResponse>(
                Result.Failure<WorkHourResponse>(WorkHourErrors.LuftIsTooBig));

        // Act
        Results<Ok<WorkHourResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.End(new WorkTimeFixRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AutoGenerate_Success_ReturnsTheCountAndPassesTheRequest()
    {
        // Arrange
        var request = new WorkHoursAutoGenerateRequest
        {
            DateFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
            DateTo = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified)
        };
        var response = new WorkHoursAutoGenerateResponse(57);
        Mock<ICommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse>> handler =
            CommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse>(response);

        // Act
        Results<Ok<WorkHoursAutoGenerateResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.AutoGenerate(request, handler.Object);

        // Assert
        Assert.Same(response, Assert.IsType<Ok<WorkHoursAutoGenerateResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new AutoGenerateWorkHoursCommand(request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AutoGenerate_NoPeriod_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<ICommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse>> handler =
            CommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse>(
                Result.Failure<WorkHoursAutoGenerateResponse>(WorkHourErrors.PeriodIsRequired));

        // Act
        Results<Ok<WorkHoursAutoGenerateResponse>, ProblemHttpResult> result =
            await WorkHoursEndpoints.AutoGenerate(new WorkHoursAutoGenerateRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/workhours/rowsdata", "GET")]
    [InlineData("api/v1/workhours/formlookups", "GET")]
    [InlineData("api/v1/workhours/{whId:int}", "GET")]
    [InlineData("api/v1/workhours/", "POST")]
    [InlineData("api/v1/workhours/{whId:int}", "PUT")]
    [InlineData("api/v1/workhours/{whId:int}", "DELETE")]
    [InlineData("api/v1/workhours/start", "POST")]
    [InlineData("api/v1/workhours/end", "POST")]
    [InlineData("api/v1/workhours/autogenerate", "POST")]
    public async Task UseAppMimosiGeApi_MapsWorkHoursEndpoints(string pattern, string method)
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
    public async Task UseWorkHoursEndpoints_MapsNineEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseWorkHoursEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(9, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseWorkHoursEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseWorkHoursEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(WorkHoursEndpoints.UseWorkHoursEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(WorkHoursEndpoints.UseWorkHoursEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the work hours endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheWorkHoursStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(WorkHoursEndpoints.UseWorkHoursEndpoints)),
            Times.Once);
    }
}
