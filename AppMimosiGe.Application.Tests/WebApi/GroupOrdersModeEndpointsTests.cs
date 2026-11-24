using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.GetGroupOrdersExportFile;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupEditing;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;
using AppMimosiGe.WebApi.DependencyInjection;
using AppMimosiGe.WebApi.Endpoints.V1;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Serilog;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using SystemTools.SystemToolsShared;
using Xunit;

namespace AppMimosiGe.Application.Tests.WebApi;

//ვიზარდების endpoint-ები (D194, D195): მენიუ „ბრძანებების რეჟიმი“ (groupOrdersMode)
public sealed class GroupOrdersModeEndpointsTests
{
    private const string FileName = "GroupOrders_20261124163000.json";

    private static readonly SwitchToGroupOrdersResponse Switched =
        new(false, true, new DateTime(2026, 11, 24, 0, 0, 0, DateTimeKind.Unspecified), [], []);

    private static Mock<ICommandHandler<TCommand, TResponse>> CommandHandler<TCommand, TResponse>(
        Result<TResponse> result) where TCommand : ICommand<TResponse>
    {
        var handler = new Mock<ICommandHandler<TCommand, TResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return handler;
    }

    private static Mock<IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile>> FileHandler(
        Result<GroupOrdersExportFile> result)
    {
        var handler = new Mock<IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile>>();
        handler.Setup(h => h.Handle(It.IsAny<GetGroupOrdersExportFileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return handler;
    }

    private static void AssertProblem(IResult result, int statusCode)
    {
        Assert.Equal(statusCode, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SwitchToGroupOrders_Success_ReturnsOkAndPassesDryRun(bool dryRun)
    {
        // Arrange
        Mock<ICommandHandler<SwitchToGroupOrdersCommand, SwitchToGroupOrdersResponse>> handler =
            CommandHandler<SwitchToGroupOrdersCommand, SwitchToGroupOrdersResponse>(Switched);

        // Act
        Results<Ok<SwitchToGroupOrdersResponse>, ProblemHttpResult> result =
            await GroupOrdersModeEndpoints.SwitchToGroupOrders(handler.Object, dryRun);

        // Assert
        Assert.Same(Switched, Assert.IsType<Ok<SwitchToGroupOrdersResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new SwitchToGroupOrdersCommand(dryRun), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SwitchToGroupOrders_AlreadyInOrdersMode_Returns409Problem()
    {
        // Arrange
        Mock<ICommandHandler<SwitchToGroupOrdersCommand, SwitchToGroupOrdersResponse>> handler =
            CommandHandler<SwitchToGroupOrdersCommand, SwitchToGroupOrdersResponse>(
                Result.Failure<SwitchToGroupOrdersResponse>(GroupOrderErrors.GroupOrdersAreAlreadyEnabled));

        // Act
        Results<Ok<SwitchToGroupOrdersResponse>, ProblemHttpResult> result =
            await GroupOrdersModeEndpoints.SwitchToGroupOrders(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task SwitchToGroupEditing_Success_ReturnsTheFileName()
    {
        // Arrange
        var response = new SwitchToGroupEditingResponse(FileName, 3);
        Mock<ICommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>> handler =
            CommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>(response);

        // Act
        Results<Ok<SwitchToGroupEditingResponse>, ProblemHttpResult> result =
            await GroupOrdersModeEndpoints.SwitchToGroupEditing(handler.Object);

        // Assert
        Assert.Same(response, Assert.IsType<Ok<SwitchToGroupEditingResponse>>(result.Result).Value);
    }

    //D195: ფაილი ვერ ჩაიწერა, არაფერი წაშლილა
    [Fact]
    public async Task SwitchToGroupEditing_FileNotWritten_Returns500Problem()
    {
        // Arrange
        Mock<ICommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>> handler =
            CommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>(
                Result.Failure<SwitchToGroupEditingResponse>(GroupOrderErrors.ExportFileWriteFailed));

        // Act
        Results<Ok<SwitchToGroupEditingResponse>, ProblemHttpResult> result =
            await GroupOrdersModeEndpoints.SwitchToGroupEditing(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task ExportFile_Found_ReturnsTheJsonFile()
    {
        // Arrange
        byte[] content = [123, 125];
        Mock<IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile>> handler =
            FileHandler(new GroupOrdersExportFile(FileName, content));

        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result =
            await GroupOrdersModeEndpoints.ExportFile(FileName, handler.Object);

        // Assert
        FileContentHttpResult file = Assert.IsType<FileContentHttpResult>(result.Result);
        Assert.Equal((FileName, "application/json"), (file.FileDownloadName, file.ContentType));
        Assert.Equal(content, file.FileContents.ToArray());
        handler.Verify(h => h.Handle(new GetGroupOrdersExportFileQuery(FileName), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ExportFile_Missing_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile>> handler =
            FileHandler(Result.Failure<GroupOrdersExportFile>(GroupOrderErrors.ExportFileNotFound));

        // Act
        Results<FileContentHttpResult, ProblemHttpResult> result =
            await GroupOrdersModeEndpoints.ExportFile(FileName, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    //SPA-ს მიერ გამოძახებული route-ები თავისი HTTP მეთოდებით; რეჟიმის წაკითხვა ბრძანებების ჯგუფშია
    [Theory]
    [InlineData("api/v1/grouporders/mode/toorders", "POST")]
    [InlineData("api/v1/grouporders/mode/togroups", "POST")]
    [InlineData("api/v1/grouporders/mode/files/{fileName}", "GET")]
    [InlineData("api/v1/grouporders/mode", "GET")]
    public async Task UseAppMimosiGeApi_MapsGroupOrdersModeEndpoints(string pattern, string method)
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseAppMimosiGeApi(null));

        // Assert
        Assert.Contains(EndpointsTestApp.MappedEndpoints(app),
            e => e.RoutePattern.RawText?.TrimStart('/') == pattern &&
                 e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public async Task UseGroupOrdersModeEndpoints_MapsThreeEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseGroupOrdersModeEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(3, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseGroupOrdersModeEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started",
            nameof(GroupOrdersModeEndpoints.UseGroupOrdersModeEndpoints)), Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished",
            nameof(GroupOrdersModeEndpoints.UseGroupOrdersModeEndpoints)), Times.Once);
    }

    //აპი ვიზარდების endpoint-ებით; Admin-ის მენიუს უფლება hasRight-ია
    private static WebApplication AppWithRights(bool hasRight, Action<IServiceCollection> addHandler)
    {
        var rights = new Mock<IUserRightsRepository>();
        rights.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        rights.Setup(r => r.CheckMenuRight(5, "Admin", 5, 5, "groupOrdersMode", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRight);
        var databaseAbstraction = new Mock<IDatabaseAbstraction>();
        databaseAbstraction.Setup(d => d.GetTableName<It.IsAnyType>()).Returns("AnyTable");
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Roles).Returns(["Admin"]);
        WebApplication app = EndpointsTestApp.Build(services =>
        {
            services.AddSingleton(rights.Object);
            services.AddSingleton(databaseAbstraction.Object);
            services.AddSingleton(currentUser.Object);
            addHandler(services);
        });
        app.UseGroupOrdersModeEndpoints(null);
        return app;
    }

    private static RequestDelegate Endpoint(WebApplication app, string pattern, string method)
    {
        return EndpointsTestApp.MappedEndpoints(app).Single(e =>
            e.RoutePattern.RawText?.TrimStart('/') == pattern &&
            e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method)).RequestDelegate!;
    }

    //რუკაზე მიბმული endpoint ჯერ ჯგუფის ფილტრს უშვებს (მენიუ „ბრძანებების რეჟიმი“, D184): უფლების გარეშე 403-ია და
    //handler-ი არ ეშვება (D152-ის ხერხი)
    [Theory]
    [InlineData(false, StatusCodes.Status403Forbidden, 0)]
    [InlineData(true, StatusCodes.Status200OK, 1)]
    public async Task SwitchToGroupEditing_RunsOnlyWithTheGroupOrdersModeMenuRight(bool hasRight, int statusCode,
        int handledTimes)
    {
        // Arrange
        Mock<ICommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>> handler =
            CommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>(
                new SwitchToGroupEditingResponse(FileName, 1));
        await using WebApplication app = AppWithRights(hasRight, services => services.AddSingleton(handler.Object));
        var httpContext = new DefaultHttpContext { RequestServices = app.Services };
        httpContext.Request.Method = HttpMethods.Post;

        // Act
        await Endpoint(app, "api/v1/grouporders/mode/togroups", HttpMethods.Post)(httpContext);

        // Assert
        Assert.Equal(statusCode, httpContext.Response.StatusCode);
        handler.Verify(h => h.Handle(new SwitchToGroupEditingCommand(), It.IsAny<CancellationToken>()),
            Times.Exactly(handledTimes));
    }

    [Theory]
    [InlineData(false, StatusCodes.Status403Forbidden, 0)]
    [InlineData(true, StatusCodes.Status200OK, 1)]
    public async Task ExportFile_RunsOnlyWithTheGroupOrdersModeMenuRight(bool hasRight, int statusCode,
        int handledTimes)
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile>> handler =
            FileHandler(new GroupOrdersExportFile(FileName, [123, 125]));
        await using WebApplication app = AppWithRights(hasRight, services => services.AddSingleton(handler.Object));
        var httpContext = new DefaultHttpContext { RequestServices = app.Services };
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.RouteValues["fileName"] = FileName;

        // Act
        await Endpoint(app, "api/v1/grouporders/mode/files/{fileName}", HttpMethods.Get)(httpContext);

        // Assert
        Assert.Equal(statusCode, httpContext.Response.StatusCode);
        handler.Verify(h => h.Handle(new GetGroupOrdersExportFileQuery(FileName), It.IsAny<CancellationToken>()),
            Times.Exactly(handledTimes));
    }
}
