using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.CreateGroupOrder;
using AppMimosiGe.Application.GroupOrders.DeleteGroupOrder;
using AppMimosiGe.Application.GroupOrders.GetGroupOrder;
using AppMimosiGe.Application.GroupOrders.GetGroupOrderFormLookups;
using AppMimosiGe.Application.GroupOrders.GetGroupOrderPrint;
using AppMimosiGe.Application.GroupOrders.GetGroupOrders;
using AppMimosiGe.Application.GroupOrders.GetGroupOrdersMode;
using AppMimosiGe.Application.GroupOrders.UpdateGroupOrder;
using AppMimosiGe.WebApi.DependencyInjection;
using AppMimosiGe.WebApi.Endpoints.V1;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
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

public sealed class GroupOrdersEndpointsTests
{
    private static readonly DateTime OrderDate = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly GroupOrderResponse Order = new(5, 3, "26.5", OrderDate.AddDays(-1), OrderDate, false, 11,
        1, null, null, null, null, null, null, "ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია", "manager",
        OrderDate.AddDays(-1), null, null, [], [], []);

    private static readonly GroupOrderPrintResponse Print = new(5, 7, "26.5", OrderDate.AddDays(-1), OrderDate,
        "ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია", "601", null, "2026-2027", "მათემატიკა", "4-ოთხი",
        "მეათე", null, null, [], [], []);

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
        var rows = new GroupOrdersRowsDataResponse(0, 0, []);
        Mock<IQueryHandler<GetGroupOrdersQuery, GroupOrdersRowsDataResponse>> handler =
            QueryHandler<GetGroupOrdersQuery, GroupOrdersRowsDataResponse>(rows);

        // Act
        Results<Ok<GroupOrdersRowsDataResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<GroupOrdersRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupOrdersQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrdersQuery, GroupOrdersRowsDataResponse>> handler =
            QueryHandler<GetGroupOrdersQuery, GroupOrdersRowsDataResponse>(
                Result.Failure<GroupOrdersRowsDataResponse>(GroupOrderErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<GroupOrdersRowsDataResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOkAndPassesTheParameters()
    {
        // Arrange
        var lookups = new GroupOrderFormLookupsResponse(11, [], [], [], [], [], [], [], [], [], [], [], [], null,
            null, null);
        Mock<IQueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>> handler =
            QueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>(lookups);

        // Act
        Results<Ok<GroupOrderFormLookupsResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetFormLookups(11, OrderDate, 1, 2, 10, 6, 3, 7, handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<GroupOrderFormLookupsResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupOrderFormLookupsQuery(11, OrderDate, 1, 2, 10, 6, 3, 7),
            It.IsAny<CancellationToken>()));
    }

    //D185: შესაცვლელი ბრძანების გადატანისას გვიანდელი ბრძანება აღარ ესადაგება
    [Fact]
    public async Task GetFormLookups_LaterOrderNoLongerFits_Returns409Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>> handler =
            QueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>(
                Result.Failure<GroupOrderFormLookupsResponse>(GroupOrderErrors.OrderNoLongerFits("26.4")));

        // Act
        Results<Ok<GroupOrderFormLookupsResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetFormLookups(null, OrderDate, 1, null, null, null, null, 3,
                handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task GetFormLookups_MissingOrderDate_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>> handler =
            QueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>(
                Result.Failure<GroupOrderFormLookupsResponse>(GroupOrderErrors.OrderDateIsRequired));

        // Act
        Results<Ok<GroupOrderFormLookupsResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetFormLookups(null, null, 1, null, null, null, null, null, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetMode_Success_ReturnsOk()
    {
        // Arrange
        var mode = new GroupOrdersModeResponse(true, OrderDate, "admin", OrderDate);
        Mock<IQueryHandler<GetGroupOrdersModeQuery, GroupOrdersModeResponse>> handler =
            QueryHandler<GetGroupOrdersModeQuery, GroupOrdersModeResponse>(mode);

        // Act
        Results<Ok<GroupOrdersModeResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetMode(handler.Object);

        // Assert
        Assert.Same(mode, Assert.IsType<Ok<GroupOrdersModeResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetMode_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrdersModeQuery, GroupOrdersModeResponse>> handler =
            QueryHandler<GetGroupOrdersModeQuery, GroupOrdersModeResponse>(
                Result.Failure<GroupOrdersModeResponse>(GroupOrderErrors.GroupOrdersAreNotEnabled));

        // Act
        Results<Ok<GroupOrdersModeResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetMode(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderQuery, GroupOrderResponse>> handler =
            QueryHandler<GetGroupOrderQuery, GroupOrderResponse>(Order);

        // Act
        Results<Ok<GroupOrderResponse>, ProblemHttpResult> result = await GroupOrdersEndpoints.GetOne(5, handler.Object);

        // Assert
        Assert.Same(Order, Assert.IsType<Ok<GroupOrderResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupOrderQuery(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderQuery, GroupOrderResponse>> handler =
            QueryHandler<GetGroupOrderQuery, GroupOrderResponse>(
                Result.Failure<GroupOrderResponse>(GroupOrderErrors.GroupOrderNotFound));

        // Act
        Results<Ok<GroupOrderResponse>, ProblemHttpResult> result = await GroupOrdersEndpoints.GetOne(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetPrint_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>> handler =
            QueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>(Print);

        // Act
        Results<Ok<GroupOrderPrintResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetPrint(5, handler.Object);

        // Assert
        Assert.Same(Print, Assert.IsType<Ok<GroupOrderPrintResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetGroupOrderPrintQuery(5), It.IsAny<CancellationToken>()));
    }

    //D193, D215: საწყისი და დაპაუზებული ბრძანება არ იბეჭდება
    [Fact]
    public async Task GetPrint_InitialOrder_Returns409Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>> handler =
            QueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>(
                Result.Failure<GroupOrderPrintResponse>(GroupOrderErrors.InitialOrderIsNotPrinted));

        // Act
        Results<Ok<GroupOrderPrintResponse>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.GetPrint(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Create_Success_ReturnsTheNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new GroupOrderRequest { OrderType = 3, OrderDate = OrderDate, GroupId = 1 };
        var handler = new Mock<ICommandHandler<CreateGroupOrderCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateGroupOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(77));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await GroupOrdersEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(77, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreateGroupOrderCommand(request), It.IsAny<CancellationToken>()));
    }

    //D183: ბრძანებები მხოლოდ ბრძანებების რეჟიმში ინახება
    [Fact]
    public async Task Create_GroupOrdersNotEnabled_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<CreateGroupOrderCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreateGroupOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(GroupOrderErrors.GroupOrdersAreNotEnabled));

        // Act
        Results<Ok<int>, ProblemHttpResult> result =
            await GroupOrdersEndpoints.Create(new GroupOrderRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new GroupOrderRequest { OrderType = 3, OrderDate = OrderDate, GroupId = 1 };
        Mock<ICommandHandler<UpdateGroupOrderCommand>> handler =
            CommandHandler<UpdateGroupOrderCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupOrdersEndpoints.Update(5, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdateGroupOrderCommand(5, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_MissingOrder_Returns404Problem()
    {
        // Arrange
        Mock<ICommandHandler<UpdateGroupOrderCommand>> handler =
            CommandHandler<UpdateGroupOrderCommand>(Result.Failure(GroupOrderErrors.GroupOrderNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result =
            await GroupOrdersEndpoints.Update(5, new GroupOrderRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<ICommandHandler<DeleteGroupOrderCommand>> handler =
            CommandHandler<DeleteGroupOrderCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupOrdersEndpoints.Delete(5, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeleteGroupOrderCommand(5), It.IsAny<CancellationToken>()));
    }

    //D196
    [Fact]
    public async Task Delete_InitialOrder_Returns409Problem()
    {
        // Arrange
        Mock<ICommandHandler<DeleteGroupOrderCommand>> handler =
            CommandHandler<DeleteGroupOrderCommand>(Result.Failure(GroupOrderErrors.InitialOrderCannotBeDeleted));

        // Act
        Results<Ok, ProblemHttpResult> result = await GroupOrdersEndpoints.Delete(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    //რუკაზე მიბმული endpoint ჯერ ჯგუფის ფილტრს უშვებს (მენიუ „ბრძანებები“, D184): უფლების გარეშე 403-ია და handler-ი
    //არ ეშვება. ფილტრი ჯგუფზეა, ამიტომ query-საც და command-საც იცავს (D152-ის ხერხი)
    [Theory]
    [InlineData(false, StatusCodes.Status403Forbidden, 0)]
    [InlineData(true, StatusCodes.Status200OK, 1)]
    public async Task GetPrint_RunsOnlyWithTheGroupOrdersMenuRight(bool hasRight, int statusCode,
        int handledTimes)
    {
        // Arrange
        Mock<IQueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>> handler =
            QueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>(Print);
        await using WebApplication app = AppWithRights(hasRight, services => services.AddSingleton(handler.Object));
        DefaultHttpContext httpContext = Request(app, HttpMethods.Get);

        // Act
        await Endpoint(app, "api/v1/grouporders/{orderId:int}/print", HttpMethods.Get)(httpContext);

        // Assert
        Assert.Equal(statusCode, httpContext.Response.StatusCode);
        handler.Verify(h => h.Handle(new GetGroupOrderPrintQuery(5), It.IsAny<CancellationToken>()),
            Times.Exactly(handledTimes));
    }

    [Theory]
    [InlineData(false, StatusCodes.Status403Forbidden, 0)]
    [InlineData(true, StatusCodes.Status200OK, 1)]
    public async Task Delete_RunsOnlyWithTheGroupOrdersMenuRight(bool hasRight, int statusCode, int handledTimes)
    {
        // Arrange
        Mock<ICommandHandler<DeleteGroupOrderCommand>> handler =
            CommandHandler<DeleteGroupOrderCommand>(Result.Success());
        await using WebApplication app = AppWithRights(hasRight, services => services.AddSingleton(handler.Object));
        DefaultHttpContext httpContext = Request(app, HttpMethods.Delete);

        // Act
        await Endpoint(app, "api/v1/grouporders/{orderId:int}", HttpMethods.Delete)(httpContext);

        // Assert
        Assert.Equal(statusCode, httpContext.Response.StatusCode);
        handler.Verify(h => h.Handle(new DeleteGroupOrderCommand(5), It.IsAny<CancellationToken>()),
            Times.Exactly(handledTimes));
    }

    //SPA-ს მიერ გამოძახებული route-ები თავისი HTTP მეთოდებით
    [Theory]
    [InlineData("api/v1/grouporders/rowsdata", "GET")]
    [InlineData("api/v1/grouporders/formlookups", "GET")]
    [InlineData("api/v1/grouporders/mode", "GET")]
    [InlineData("api/v1/grouporders/{orderId:int}", "GET")]
    [InlineData("api/v1/grouporders/{orderId:int}/print", "GET")]
    [InlineData("api/v1/grouporders/", "POST")]
    [InlineData("api/v1/grouporders/{orderId:int}", "PUT")]
    [InlineData("api/v1/grouporders/{orderId:int}", "DELETE")]
    public async Task UseAppMimosiGeApi_MapsGroupOrdersEndpoints(string pattern, string method)
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
    public async Task UseGroupOrdersEndpoints_MapsEightEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UseGroupOrdersEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(8, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UseGroupOrdersEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseGroupOrdersEndpoints(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started", nameof(GroupOrdersEndpoints.UseGroupOrdersEndpoints)),
            Times.Once);
        logger.Verify(
            l => l.Information("{MethodName} Finished", nameof(GroupOrdersEndpoints.UseGroupOrdersEndpoints)),
            Times.Once);
    }

    //UseAppMimosiGeApi თავის debug logger-ს ბრძანებების endpoint-ებსაც გადასცემს
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsTheGroupOrdersStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(
            l => l.Information("{MethodName} Started", nameof(GroupOrdersEndpoints.UseGroupOrdersEndpoints)),
            Times.Once);
    }

    //აპი ბრძანებების endpoint-ებით; Manager-ის მენიუს უფლება hasRight-ია
    private static WebApplication AppWithRights(bool hasRight, Action<IServiceCollection> addHandler)
    {
        var rights = new Mock<IUserRightsRepository>();
        rights.Setup(r => r.GetDataTypeIdByKey(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        rights.Setup(r => r.CheckMenuRight(5, "Manager", 5, 5, "groupOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRight);
        var databaseAbstraction = new Mock<IDatabaseAbstraction>();
        databaseAbstraction.Setup(d => d.GetTableName<It.IsAnyType>()).Returns("AnyTable");
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.Roles).Returns(["Manager"]);
        WebApplication app = EndpointsTestApp.Build(services =>
        {
            services.AddSingleton(rights.Object);
            services.AddSingleton(databaseAbstraction.Object);
            services.AddSingleton(currentUser.Object);
            addHandler(services);
        });
        app.UseGroupOrdersEndpoints(null);
        return app;
    }

    private static DefaultHttpContext Request(WebApplication app, string method)
    {
        var httpContext = new DefaultHttpContext { RequestServices = app.Services };
        httpContext.Request.Method = method;
        httpContext.Request.RouteValues["orderId"] = "5";
        return httpContext;
    }

    private static RequestDelegate Endpoint(WebApplication app, string pattern, string method)
    {
        return EndpointsTestApp.MappedEndpoints(app).Single(e =>
            e.RoutePattern.RawText?.TrimStart('/') == pattern &&
            e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method)).RequestDelegate!;
    }
}
