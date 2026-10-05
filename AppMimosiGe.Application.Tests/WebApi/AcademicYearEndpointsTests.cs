using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;
using AppMimosiGe.Application.AcademicYears.CreateAcademicYear;
using AppMimosiGe.Application.AcademicYears.GetAcademicYears;
using AppMimosiGe.Application.AcademicYears.GetAcademicYearWizardInfo;
using AppMimosiGe.Application.StudentContracts.GetStudentContractNextNumber;
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

public sealed class AcademicYearEndpointsTests
{
    private static readonly DateTime CloseDate = new(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static Mock<ICommandHandler<TCommand, TResponse>> CommandHandler<TCommand, TResponse>(
        Result<TResponse> result) where TCommand : ICommand<TResponse>
    {
        var handler = new Mock<ICommandHandler<TCommand, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static Mock<IQueryHandler<TQuery, TResponse>> QueryHandler<TQuery, TResponse>(Result<TResponse> result)
        where TQuery : IQuery<TResponse>
    {
        var handler = new Mock<IQueryHandler<TQuery, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    [Fact]
    public async Task GetInfo_Success_ReturnsOk()
    {
        var info = new AcademicYearWizardInfoResponse([], 11, null, null);
        Mock<IQueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse>> handler =
            QueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse>(info);

        Results<Ok<AcademicYearWizardInfoResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.GetInfo(handler.Object);

        Assert.Same(info, Assert.IsType<Ok<AcademicYearWizardInfoResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetInfo_Failure_ReturnsTheProblem()
    {
        Mock<IQueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse>> handler =
            QueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse>(
                Result.Failure<AcademicYearWizardInfoResponse>(AcademicYearErrors.NoAcademicYears));

        Results<Ok<AcademicYearWizardInfoResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.GetInfo(handler.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_Success_ReturnsOkAndPassesTheDryRun(bool dryRun)
    {
        var newYear = new NewAcademicYearResponse(dryRun, null, "2027-2028", CloseDate, CloseDate.AddYears(1), "7",
            false);
        Mock<ICommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse>> handler =
            CommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse>(newYear);

        Results<Ok<NewAcademicYearResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.Create(handler.Object, dryRun);

        Assert.Same(newYear, Assert.IsType<Ok<NewAcademicYearResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateAcademicYearCommand(dryRun), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ExistingYear_Returns409()
    {
        Mock<ICommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse>> handler =
            CommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse>(
                Result.Failure<NewAcademicYearResponse>(AcademicYearErrors.AcademicYearAlreadyExists));

        Results<Ok<NewAcademicYearResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.Create(handler.Object);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        handler.Verify(h => h.Handle(new CreateAcademicYearCommand(false), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseGroups_Success_ReturnsOkAndPassesTheYearDateAndDryRun()
    {
        var closed = new CloseAcademicYearGroupsResponse(true, 11, CloseDate, CloseDate.AddMonths(3), []);
        Mock<ICommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>> handler =
            CommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>(closed);

        Results<Ok<CloseAcademicYearGroupsResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.CloseGroups(11, handler.Object, CloseDate, true);

        Assert.Same(closed, Assert.IsType<Ok<CloseAcademicYearGroupsResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CloseAcademicYearGroupsCommand(11, CloseDate, true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseGroups_UnknownYear_Returns404()
    {
        Mock<ICommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>> handler =
            CommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>(
                Result.Failure<CloseAcademicYearGroupsResponse>(AcademicYearErrors.AcademicYearNotFound));

        Results<Ok<CloseAcademicYearGroupsResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.CloseGroups(99, handler.Object);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        handler.Verify(h => h.Handle(new CloseAcademicYearGroupsCommand(99, null, false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAcademicYears_Success_ReturnsOk()
    {
        var years = new AcademicYearsResponse([new LookupItemResponse(11, "2026-2027")], 11);
        Mock<IQueryHandler<GetAcademicYearsQuery, AcademicYearsResponse>> handler =
            QueryHandler<GetAcademicYearsQuery, AcademicYearsResponse>(years);

        Results<Ok<AcademicYearsResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.GetAcademicYears(handler.Object);

        Assert.Same(years, Assert.IsType<Ok<AcademicYearsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetAcademicYears_Failure_ReturnsTheProblem()
    {
        Mock<IQueryHandler<GetAcademicYearsQuery, AcademicYearsResponse>> handler =
            QueryHandler<GetAcademicYearsQuery, AcademicYearsResponse>(
                Result.Failure<AcademicYearsResponse>(AcademicYearErrors.NoAcademicYears));

        Results<Ok<AcademicYearsResponse>, ProblemHttpResult> result =
            await AcademicYearEndpoints.GetAcademicYears(handler.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task GetNextNumber_Success_ReturnsOkAndPassesTheYear()
    {
        var next = new StudentContractNextNumberResponse("7.001");
        Mock<IQueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse>> handler =
            QueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse>(next);

        Results<Ok<StudentContractNextNumberResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetNextNumber(12, handler.Object);

        Assert.Same(next, Assert.IsType<Ok<StudentContractNextNumberResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetStudentContractNextNumberQuery(12), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetNextNumber_UnknownYear_Returns400()
    {
        Mock<IQueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse>> handler =
            QueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse>(
                Result.Failure<StudentContractNextNumberResponse>(StudentContractErrors.AcademicYearNotFound));

        Results<Ok<StudentContractNextNumberResponse>, ProblemHttpResult> result =
            await StudentContractsEndpoints.GetNextNumber(99, handler.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/academicyear/info", "GET")]
    [InlineData("api/v1/academicyear/create", "POST")]
    [InlineData("api/v1/academicyear/{ayId:int}/closegroups", "POST")]
    [InlineData("api/v1/academicyears/", "GET")]
    [InlineData("api/v1/studentcontracts/nextnumber", "GET")]
    public async Task UseAppMimosiGeApi_MapsTheAcademicYearEndpoints(string pattern, string method)
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

    // the wizard needs the menu item's right; the list of years only a signed-in user (every page shows it)
    [Fact]
    public async Task UseAcademicYearEndpoints_MapsFourEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseAcademicYearEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(4, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        Assert.Equal(3, endpoints.Count(e => e.RoutePattern.RawText!.Contains("/academicyear/",
            StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UseAcademicYearEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAcademicYearEndpoints(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started", nameof(AcademicYearEndpoints.UseAcademicYearEndpoints)),
            Times.Once);
        logger.Verify(
            l => l.Information("{MethodName} Finished", nameof(AcademicYearEndpoints.UseAcademicYearEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the academic year endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheAcademicYearStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started", nameof(AcademicYearEndpoints.UseAcademicYearEndpoints)),
            Times.Once);
    }
}
