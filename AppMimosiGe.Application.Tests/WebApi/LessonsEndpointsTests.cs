using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons.GetLesson;
using AppMimosiGe.Application.Lessons.GetLessonFormLookups;
using AppMimosiGe.Application.Lessons.GetLessonsRowsData;
using AppMimosiGe.Application.Lessons.UpdateLesson;
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

public sealed class LessonsEndpointsTests
{
    private static readonly DateTime LessonDt = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Unspecified);

    private static readonly LessonResponse Lesson = new(9, 7, "1001", "Math", 3, "Alpha Ann / T3.01", LessonDt,
        "Senior", 8f, LessonDt, LessonDt, 1, null, 0, null, null, null, null, []);

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
        var rows = new LessonsRowsDataResponse(0, 0, []);
        Mock<IQueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse>> handler =
            QueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse>(rows);

        // Act
        Results<Ok<LessonsRowsDataResponse>, ProblemHttpResult> result =
            await LessonsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<LessonsRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetLessonsRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse>> handler =
            QueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse>(
                Result.Failure<LessonsRowsDataResponse>(LessonErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<LessonsRowsDataResponse>, ProblemHttpResult> result =
            await LessonsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new LessonFormLookupsResponse([], [], []);
        Mock<IQueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>> handler =
            QueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>(lookups);

        // Act
        Results<Ok<LessonFormLookupsResponse>, ProblemHttpResult> result =
            await LessonsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<LessonFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>> handler =
            QueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>(
                Result.Failure<LessonFormLookupsResponse>(LessonErrors.LessonNotFound));

        // Act
        Results<Ok<LessonFormLookupsResponse>, ProblemHttpResult> result =
            await LessonsEndpoints.GetFormLookups(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetLessonQuery, LessonResponse>> handler =
            QueryHandler<GetLessonQuery, LessonResponse>(Lesson);

        // Act
        Results<Ok<LessonResponse>, ProblemHttpResult> result = await LessonsEndpoints.GetOne(9, handler.Object);

        // Assert
        Assert.Same(Lesson, Assert.IsType<Ok<LessonResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetLessonQuery(9), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetLessonQuery, LessonResponse>> handler =
            QueryHandler<GetLessonQuery, LessonResponse>(Result.Failure<LessonResponse>(LessonErrors.LessonNotFound));

        // Act
        Results<Ok<LessonResponse>, ProblemHttpResult> result = await LessonsEndpoints.GetOne(9, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new LessonRequest { LessonStatusId = 2 };
        var handler = new Mock<ICommandHandler<UpdateLessonCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateLessonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await LessonsEndpoints.Update(9, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateLessonCommand(9, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_RowOfAnotherLesson_Returns400Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<UpdateLessonCommand>>();
        handler.Setup(h => h.Handle(It.IsAny<UpdateLessonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(LessonErrors.StudentRowNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result =
            await LessonsEndpoints.Update(9, new LessonRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    // the routes the SPA calls, with their HTTP methods. There is no create and no delete: the generator owns them
    [Theory]
    [InlineData("api/v1/lessons/rowsdata", "GET")]
    [InlineData("api/v1/lessons/formlookups", "GET")]
    [InlineData("api/v1/lessons/{lessonId:int}", "GET")]
    [InlineData("api/v1/lessons/{lessonId:int}", "PUT")]
    public async Task UseAppMimosiGeApi_MapsLessonsEndpoints(string pattern, string method)
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
    public async Task UseLessonsEndpoints_MapsFourEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseLessonsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(4, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseLessonsEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseLessonsEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(LessonsEndpoints.UseLessonsEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(LessonsEndpoints.UseLessonsEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the lessons endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheLessonsStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(LessonsEndpoints.UseLessonsEndpoints)),
            Times.Once);
    }
}
