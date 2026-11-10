using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.GetBankTransaction;
using AppMimosiGe.Application.BankTransactions.GetBankTransactions;
using AppMimosiGe.Application.BankTransactions.GetBankTransactionStudentContracts;
using AppMimosiGe.Application.BankTransactions.ImportBankTransactions;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionIgnored;
using AppMimosiGe.WebApi.DependencyInjection;
using AppMimosiGe.WebApi.Endpoints.V1;
using AppMimosiGe.WebApi.Filters;
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

public sealed class BankTransactionsEndpointsTests
{
    private static readonly DateTime Day = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Unspecified);

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
    public async Task GetList_PassesTheParametersAndMapsTheResult()
    {
        List<BankTransactionRowResponse> rows = [];
        var handler = QueryHandler<GetBankTransactionsQuery, List<BankTransactionRowResponse>>(rows);

        var ok = await BankTransactionsEndpoints.GetList(Day, Day.AddDays(1), "unallocated", handler.Object);

        Assert.Same(rows, Assert.IsType<Ok<List<BankTransactionRowResponse>>>(ok.Result).Value);
        handler.Verify(h => h.Handle(new GetBankTransactionsQuery(Day, Day.AddDays(1), "unallocated"),
            It.IsAny<CancellationToken>()));

        handler = QueryHandler<GetBankTransactionsQuery, List<BankTransactionRowResponse>>(
            Result.Failure<List<BankTransactionRowResponse>>(BankTransactionErrors.ListFilterIsInvalid));
        AssertProblem((await BankTransactionsEndpoints.GetList(null, null, "x", handler.Object)).Result,
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetStudentContracts_PassesTheYear()
    {
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann 6.001")];
        var handler = QueryHandler<GetBankTransactionStudentContractsQuery, List<LookupItemResponse>>(contracts);

        var ok = await BankTransactionsEndpoints.GetStudentContracts(11, handler.Object);

        Assert.Same(contracts, Assert.IsType<Ok<List<LookupItemResponse>>>(ok.Result).Value);
        handler.Verify(h => h.Handle(new GetBankTransactionStudentContractsQuery(11), It.IsAny<CancellationToken>()));

        handler = QueryHandler<GetBankTransactionStudentContractsQuery, List<LookupItemResponse>>(
            Result.Failure<List<LookupItemResponse>>(BankTransactionErrors.PeriodIsInvalid));
        AssertProblem((await BankTransactionsEndpoints.GetStudentContracts(11, handler.Object)).Result,
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOne_OkOrNotFound()
    {
        BankTransactionResponse response = new(7, "თიბისი", Day, 10m, "GEL", null, null, null, null, null, null, null, 0, false,
            false, []);
        var handler = QueryHandler<GetBankTransactionQuery, BankTransactionResponse>(response);

        var ok = await BankTransactionsEndpoints.GetOne(7, handler.Object);

        Assert.Same(response, Assert.IsType<Ok<BankTransactionResponse>>(ok.Result).Value);
        handler.Verify(h => h.Handle(new GetBankTransactionQuery(7), It.IsAny<CancellationToken>()));

        handler = QueryHandler<GetBankTransactionQuery, BankTransactionResponse>(
            Result.Failure<BankTransactionResponse>(BankTransactionErrors.BankTransactionNotFound));
        AssertProblem((await BankTransactionsEndpoints.GetOne(8, handler.Object)).Result,
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Import_PassesThePeriodAndDryRun()
    {
        ImportBankTransactionsResponse response = new([new ImportBankAccountResponse(4, "თიბისი", Day, Day, 1, 0, 1, 1, 0,
            null)], true);
        var handler = new Mock<ICommandHandler<ImportBankTransactionsCommand, ImportBankTransactionsResponse>>();
        handler.Setup(h => h.Handle(It.IsAny<ImportBankTransactionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var ok = await BankTransactionsEndpoints.Import(Day, Day.AddDays(1), true, handler.Object);
        await BankTransactionsEndpoints.Import(null, null, null, handler.Object);

        Assert.Same(response, Assert.IsType<Ok<ImportBankTransactionsResponse>>(ok.Result).Value);
        handler.Verify(h => h.Handle(new ImportBankTransactionsCommand(Day, Day.AddDays(1), true),
            It.IsAny<CancellationToken>()));
        handler.Verify(h => h.Handle(new ImportBankTransactionsCommand(null, null, false),
            It.IsAny<CancellationToken>()));

        handler.Setup(h => h.Handle(It.IsAny<ImportBankTransactionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ImportBankTransactionsResponse>(BankTransactionErrors.ImportIsRunning));
        AssertProblem((await BankTransactionsEndpoints.Import(null, null, null, handler.Object)).Result,
            StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task SetAllocation_PassesTheRequest()
    {
        BankTransactionAllocationRequest request = new() { Shares = [new() { StudentContractId = 10, Amount = 5m }] };
        var handler = CommandHandler<SetBankTransactionAllocationCommand>(Result.Success());

        var ok = await BankTransactionsEndpoints.SetAllocation(7, request, handler.Object);

        Assert.IsType<Ok>(ok.Result);
        handler.Verify(h => h.Handle(new SetBankTransactionAllocationCommand(7, request),
            It.IsAny<CancellationToken>()));

        handler = CommandHandler<SetBankTransactionAllocationCommand>(
            Result.Failure(BankTransactionErrors.AllocationExceedsAmount));
        AssertProblem((await BankTransactionsEndpoints.SetAllocation(7, request, handler.Object)).Result,
            StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task SetIgnored_PassesTheFlag()
    {
        var handler = CommandHandler<SetBankTransactionIgnoredCommand>(Result.Success());

        var ok = await BankTransactionsEndpoints.SetIgnored(7, new BankTransactionIgnoredRequest { Ignored = true },
            handler.Object);

        Assert.IsType<Ok>(ok.Result);
        handler.Verify(h => h.Handle(new SetBankTransactionIgnoredCommand(7, true), It.IsAny<CancellationToken>()));

        handler = CommandHandler<SetBankTransactionIgnoredCommand>(
            Result.Failure(BankTransactionErrors.BankTransactionHasPayments));
        AssertProblem((await BankTransactionsEndpoints.SetIgnored(7, new BankTransactionIgnoredRequest(),
            handler.Object)).Result, StatusCodes.Status409Conflict);
    }

    // the routes the SPA calls, with their HTTP methods
    [Theory]
    [InlineData("api/v1/banktransactions/", "GET")]
    [InlineData("api/v1/banktransactions/studentcontracts", "GET")]
    [InlineData("api/v1/banktransactions/{bankTransactionId:int}", "GET")]
    [InlineData("api/v1/banktransactions/import", "POST")]
    [InlineData("api/v1/banktransactions/{bankTransactionId:int}/allocation", "PUT")]
    [InlineData("api/v1/banktransactions/{bankTransactionId:int}/ignored", "PUT")]
    public async Task UseAppMimosiGeApi_MapsTheBankTransactionsEndpoints(string pattern, string method)
    {
        await using WebApplication app = EndpointsTestApp.Build();

        Assert.True(app.UseAppMimosiGeApi(null));

        Assert.Contains(EndpointsTestApp.MappedEndpoints(app),
            e => e.RoutePattern.RawText?.TrimStart('/') == pattern &&
                 e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public async Task UseBankTransactionsEndpoints_MapsSixEndpointsWithAuthorizationAndTheMenuRight()
    {
        await using WebApplication app = EndpointsTestApp.Build();

        Assert.True(app.UseBankTransactionsEndpoints(null));

        List<RouteEndpoint> endpoints = EndpointsTestApp.MappedEndpoints(app);
        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, e => Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        Assert.Equal("bankTransactions", UserMustHaveBankTransactionsRightsFilter.MenuKey);
    }

    [Fact]
    public async Task UseBankTransactionsEndpoints_WithDebugLogger_LogsStartAndFinish()
    {
        await using WebApplication app = EndpointsTestApp.Build();
        var logger = new Mock<ILogger>();

        app.UseAppMimosiGeApi(logger.Object);

        logger.Verify(l => l.Information("{MethodName} Started",
            nameof(BankTransactionsEndpoints.UseBankTransactionsEndpoints)), Times.Once);
        logger.Verify(l => l.Information("{MethodName} Finished",
            nameof(BankTransactionsEndpoints.UseBankTransactionsEndpoints)), Times.Once);
    }
}
