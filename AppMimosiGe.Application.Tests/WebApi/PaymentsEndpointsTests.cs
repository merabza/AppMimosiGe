using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments.CreatePayment;
using AppMimosiGe.Application.Payments.DeletePayment;
using AppMimosiGe.Application.Payments.GetPayment;
using AppMimosiGe.Application.Payments.GetPaymentFormLookups;
using AppMimosiGe.Application.Payments.GetPaymentsRowsData;
using AppMimosiGe.Application.Payments.GetPaymentStudentContracts;
using AppMimosiGe.Application.Payments.UpdatePayment;
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

public sealed class PaymentsEndpointsTests
{
    private static readonly PaymentResponse Payment = new(5, 10, "Alpha Ann 6.001", 11,
        new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified), 300m, null, 1, false);

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
        var rows = new PaymentsRowsDataResponse(0, 0, 0m, []);
        Mock<IQueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse>> handler =
            QueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse>(rows);

        // Act
        Results<Ok<PaymentsRowsDataResponse>, ProblemHttpResult> result =
            await PaymentsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        Assert.Same(rows, Assert.IsType<Ok<PaymentsRowsDataResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetPaymentsRowsDataQuery("abc"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetRowsData_Failure_ReturnsBadRequestProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse>> handler =
            QueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse>(
                Result.Failure<PaymentsRowsDataResponse>(PaymentErrors.FilterSortRequestIsInvalid));

        // Act
        Results<Ok<PaymentsRowsDataResponse>, ProblemHttpResult> result =
            await PaymentsEndpoints.GetRowsData("abc", handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetFormLookups_Success_ReturnsOk()
    {
        // Arrange
        var lookups = new PaymentFormLookupsResponse(11, [], []);
        Mock<IQueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse>> handler =
            QueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse>(lookups);

        // Act
        Results<Ok<PaymentFormLookupsResponse>, ProblemHttpResult> result =
            await PaymentsEndpoints.GetFormLookups(handler.Object);

        // Assert
        Assert.Same(lookups, Assert.IsType<Ok<PaymentFormLookupsResponse>>(result.Result).Value);
    }

    [Fact]
    public async Task GetFormLookups_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse>> handler =
            QueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse>(
                Result.Failure<PaymentFormLookupsResponse>(PaymentErrors.PaymentNotFound));

        // Act
        Results<Ok<PaymentFormLookupsResponse>, ProblemHttpResult> result =
            await PaymentsEndpoints.GetFormLookups(handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetStudentContracts_Success_ReturnsOkAndPassesTheYear()
    {
        // Arrange
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann 6.001")];
        Mock<IQueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>>> handler =
            QueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>>(contracts);

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await PaymentsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        Assert.Same(contracts, Assert.IsType<Ok<List<LookupItemResponse>>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetPaymentStudentContractsQuery(11), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetStudentContracts_Failure_ReturnsProblem()
    {
        // Arrange
        Mock<IQueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>>> handler =
            QueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>>(
                Result.Failure<List<LookupItemResponse>>(PaymentErrors.StudentContractNotFound));

        // Act
        Results<Ok<List<LookupItemResponse>>, ProblemHttpResult> result =
            await PaymentsEndpoints.GetStudentContracts(11, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOne_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<IQueryHandler<GetPaymentQuery, PaymentResponse>> handler =
            QueryHandler<GetPaymentQuery, PaymentResponse>(Payment);

        // Act
        Results<Ok<PaymentResponse>, ProblemHttpResult> result = await PaymentsEndpoints.GetOne(5, handler.Object);

        // Assert
        Assert.Same(Payment, Assert.IsType<Ok<PaymentResponse>>(result.Result).Value);
        handler.Verify(h => h.Handle(new GetPaymentQuery(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetOne_NotFound_Returns404Problem()
    {
        // Arrange
        Mock<IQueryHandler<GetPaymentQuery, PaymentResponse>> handler =
            QueryHandler<GetPaymentQuery, PaymentResponse>(
                Result.Failure<PaymentResponse>(PaymentErrors.PaymentNotFound));

        // Act
        Results<Ok<PaymentResponse>, ProblemHttpResult> result = await PaymentsEndpoints.GetOne(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_Success_ReturnsTheNewIdAndPassesTheRequest()
    {
        // Arrange
        var request = new PaymentRequest { StudentContractId = 10 };
        var handler = new Mock<ICommandHandler<CreatePaymentCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreatePaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(77));

        // Act
        Results<Ok<int>, ProblemHttpResult> result = await PaymentsEndpoints.Create(request, handler.Object);

        // Assert
        Assert.Equal(77, Assert.IsType<Ok<int>>(result.Result).Value);
        handler.Verify(h => h.Handle(new CreatePaymentCommand(request), It.IsAny<CancellationToken>()));
    }

    // setting the checked flag without the right is a conflict with the payment's rules
    [Fact]
    public async Task Create_CheckedWithoutTheRight_Returns409Problem()
    {
        // Arrange
        var handler = new Mock<ICommandHandler<CreatePaymentCommand, int>>();
        handler.Setup(h => h.Handle(It.IsAny<CreatePaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(PaymentErrors.CheckedRequiresRight));

        // Act
        Results<Ok<int>, ProblemHttpResult> result =
            await PaymentsEndpoints.Create(new PaymentRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Update_Success_ReturnsOkAndPassesIdAndRequest()
    {
        // Arrange
        var request = new PaymentRequest { StudentContractId = 10 };
        Mock<ICommandHandler<UpdatePaymentCommand>> handler = CommandHandler<UpdatePaymentCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await PaymentsEndpoints.Update(5, request, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new UpdatePaymentCommand(5, request), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_CheckedPaymentWithoutTheRight_Returns409Problem()
    {
        // Arrange
        Mock<ICommandHandler<UpdatePaymentCommand>> handler =
            CommandHandler<UpdatePaymentCommand>(Result.Failure(PaymentErrors.PaymentIsChecked));

        // Act
        Results<Ok, ProblemHttpResult> result = await PaymentsEndpoints.Update(5, new PaymentRequest(), handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Delete_Success_ReturnsOkAndPassesTheId()
    {
        // Arrange
        Mock<ICommandHandler<DeletePaymentCommand>> handler = CommandHandler<DeletePaymentCommand>(Result.Success());

        // Act
        Results<Ok, ProblemHttpResult> result = await PaymentsEndpoints.Delete(5, handler.Object);

        // Assert
        Assert.IsType<Ok>(result.Result);
        handler.Verify(h => h.Handle(new DeletePaymentCommand(5), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_MissingPayment_Returns404Problem()
    {
        // Arrange
        Mock<ICommandHandler<DeletePaymentCommand>> handler =
            CommandHandler<DeletePaymentCommand>(Result.Failure(PaymentErrors.PaymentNotFound));

        // Act
        Results<Ok, ProblemHttpResult> result = await PaymentsEndpoints.Delete(5, handler.Object);

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/payments/rowsdata", "GET")]
    [InlineData("api/v1/payments/formlookups", "GET")]
    [InlineData("api/v1/payments/studentcontracts", "GET")]
    [InlineData("api/v1/payments/{paymentId:int}", "GET")]
    [InlineData("api/v1/payments/", "POST")]
    [InlineData("api/v1/payments/{paymentId:int}", "PUT")]
    [InlineData("api/v1/payments/{paymentId:int}", "DELETE")]
    public async Task UseAppMimosiGeApi_MapsPaymentsEndpoints(string pattern, string method)
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
    public async Task UsePaymentsEndpoints_MapsSevenEndpointsThatRequireAuthorization()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();

        // Act
        Assert.True(app.UsePaymentsEndpoints(null));

        // Assert
        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(7, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task UsePaymentsEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UsePaymentsEndpoints(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(PaymentsEndpoints.UsePaymentsEndpoints)),
            Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished", nameof(PaymentsEndpoints.UsePaymentsEndpoints)),
            Times.Once);
    }

    // UseAppMimosiGeApi hands its debug logger on to the payments endpoints
    [Fact]
    public async Task UseAppMimosiGeApi_WithDebugLogger_LogsThePaymentsStep()
    {
        // Arrange
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        // Act
        app.UseAppMimosiGeApi(logger.Object);

        // Assert
        logger.Verify(l => l.Information("{MethodName} Started", nameof(PaymentsEndpoints.UsePaymentsEndpoints)),
            Times.Once);
    }
}
