using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLastLesson;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLessons;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;
using AppMimosiGe.Application.LessonGenerator.GetLessonGeneratorLog;
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

public sealed class LessonGeneratorEndpointsTests
{
    private static readonly LessonsGenerationResponse Generation = new(false,
        new DateTime(2027, 11, 30, 0, 0, 0, DateTimeKind.Unspecified), 0, []);

    private static Mock<ICommandHandler<TCommand, TResponse>> CommandHandler<TCommand, TResponse>(
        Result<TResponse> result) where TCommand : ICommand<TResponse>
    {
        var handler = new Mock<ICommandHandler<TCommand, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateGroupLessons_Success_ReturnsOkAndPassesTheGroupAndDryRun(bool dryRun)
    {
        // Arrange
        Mock<ICommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse>> handler =
            CommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse>(Generation);

        // Act
        Results<Ok<LessonsGenerationResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateGroupLessons(7, handler.Object, dryRun);

        // Assert
        Assert.Same(Generation, Assert.IsType<Ok<LessonsGenerationResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GenerateGroupLessonsCommand(7, dryRun), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateGroupLessons_NotFound_Returns404Problem()
    {
        Mock<ICommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse>> handler =
            CommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse>(
                Result.Failure<LessonsGenerationResponse>(GroupErrors.GroupNotFound));

        Results<Ok<LessonsGenerationResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateGroupLessons(7, handler.Object);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task GenerateGroupLastLesson_Success_ReturnsOkAndPassesTheGroup()
    {
        // Arrange
        var lastLesson = new GroupLastLessonResponse(100, new DateTime(2026, 9, 28, 15, 0, 0, DateTimeKind.Unspecified),
            Generation);
        Mock<ICommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse>> handler =
            CommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse>(lastLesson);

        // Act
        Results<Ok<GroupLastLessonResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateGroupLastLesson(7, handler.Object);

        // Assert
        Assert.Same(lastLesson, Assert.IsType<Ok<GroupLastLessonResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GenerateGroupLastLessonCommand(7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateGroupLastLesson_NotFound_Returns404Problem()
    {
        Mock<ICommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse>> handler =
            CommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse>(
                Result.Failure<GroupLastLessonResponse>(GroupErrors.GroupNotFound));

        Results<Ok<GroupLastLessonResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateGroupLastLesson(7, handler.Object);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateDirtyGroupsLessons_PassesOnlyDirtyAndDryRun(bool dryRun)
    {
        Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>> handler =
            CommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>(Generation);

        Results<Ok<LessonsGenerationResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateDirtyGroupsLessons(handler.Object, dryRun);

        Assert.Same(Generation, Assert.IsType<Ok<LessonsGenerationResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GenerateGroupsLessonsCommand(true, dryRun), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateAllGroupsLessons_PassesAllGroupsAndDryRun(bool dryRun)
    {
        Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>> handler =
            CommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>(Generation);

        Results<Ok<LessonsGenerationResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateAllGroupsLessons(handler.Object, dryRun);

        Assert.Same(Generation, Assert.IsType<Ok<LessonsGenerationResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GenerateGroupsLessonsCommand(false, dryRun), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAllGroupsLessons_Failure_ReturnsProblem()
    {
        Mock<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>> handler =
            CommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>(
                Result.Failure<LessonsGenerationResponse>(GroupErrors.GroupNotFound));

        Results<Ok<LessonsGenerationResponse>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GenerateAllGroupsLessons(handler.Object);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    public async Task GetLog_Success_ReturnsOkAndPassesTheGroup(int? grpId)
    {
        // Arrange
        List<LessonGeneratorLogRowResponse> rows =
        [
            new(1, new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Unspecified), 7, "1001", 6, "text",
                new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified), null)
        ];
        var handler = new Mock<IQueryHandler<GetLessonGeneratorLogQuery, List<LessonGeneratorLogRowResponse>>>();
        handler.Setup(h => h.Handle(It.IsAny<GetLessonGeneratorLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        // Act
        Results<Ok<List<LessonGeneratorLogRowResponse>>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GetLog(handler.Object, grpId);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<List<LessonGeneratorLogRowResponse>>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetLessonGeneratorLogQuery(grpId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLog_Failure_ReturnsProblem()
    {
        var handler = new Mock<IQueryHandler<GetLessonGeneratorLogQuery, List<LessonGeneratorLogRowResponse>>>();
        handler.Setup(h => h.Handle(It.IsAny<GetLessonGeneratorLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<List<LessonGeneratorLogRowResponse>>(GroupErrors.GroupNotFound));

        Results<Ok<List<LessonGeneratorLogRowResponse>>, ProblemHttpResult> result =
            await LessonGeneratorEndpoints.GetLog(handler.Object);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/lessongenerator/groups/{grpId:int}", "POST")]
    [InlineData("api/v1/lessongenerator/groups/{grpId:int}/lastlesson", "POST")]
    [InlineData("api/v1/lessongenerator/dirtygroups", "POST")]
    [InlineData("api/v1/lessongenerator/allgroups", "POST")]
    [InlineData("api/v1/lessongenerator/log", "GET")]
    public async Task UseAppMimosiGeApi_MapsLessonGeneratorEndpoints(string pattern, string method)
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
    public async Task UseLessonGeneratorEndpoints_MapsFiveEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseLessonGeneratorEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(5, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseLessonGeneratorEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseLessonGeneratorEndpoints(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started", nameof(LessonGeneratorEndpoints.UseLessonGeneratorEndpoints)),
            Times.Once);
        logger.Verify(
            l => l.Information("{MethodName} Finished", nameof(LessonGeneratorEndpoints.UseLessonGeneratorEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the lesson generator endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheLessonGeneratorStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started", nameof(LessonGeneratorEndpoints.UseLessonGeneratorEndpoints)),
            Times.Once);
        logger.Verify(
            l => l.Information("{MethodName} Finished", nameof(LessonGeneratorEndpoints.UseLessonGeneratorEndpoints)),
            Times.Once);
    }
}
