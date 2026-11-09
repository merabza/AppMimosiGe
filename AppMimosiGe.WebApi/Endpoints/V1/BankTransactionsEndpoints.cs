using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.GetBankTransaction;
using AppMimosiGe.Application.BankTransactions.GetBankTransactions;
using AppMimosiGe.Application.BankTransactions.GetBankTransactionStudentContracts;
using AppMimosiGe.Application.BankTransactions.ImportBankTransactions;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionIgnored;
using AppMimosiGe.WebApi.Filters;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Serilog;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using WebSystemTools.WebApi.Abstractions.Infrastructure;
using Routes = AppMimosiGeShared.Contracts.V1.Routes.AppMimosiGeApiRoutes;

namespace AppMimosiGe.WebApi.Endpoints.V1;

//ბანკის ჩარიცხვები და მათი განაწილება მოსწავლეებზე (TBC_INTEGRATION.md, D164)
public static class BankTransactionsEndpoints
{
    public static bool UseBankTransactionsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseBankTransactionsEndpoints));

        RouteGroupBuilder group = endpoints
            .MapGroup(Routes.ApiBase + Routes.BankTransactionsRoute.BankTransactionsBase).RequireAuthorization()
            .AddEndpointFilter<UserMustHaveBankTransactionsRightsFilter>();

        group.MapGet(Routes.BankTransactionsRoute.List, GetList);
        group.MapGet(Routes.BankTransactionsRoute.StudentContracts, GetStudentContracts);
        group.MapGet(Routes.BankTransactionsRoute.GetOne, GetOne);
        group.MapPost(Routes.BankTransactionsRoute.Import, Import);
        group.MapPut(Routes.BankTransactionsRoute.Allocation, SetAllocation);
        group.MapPut(Routes.BankTransactionsRoute.Ignored, SetIgnored);

        debugLogger?.Information("{MethodName} Finished", nameof(UseBankTransactionsEndpoints));
        return true;
    }

    // GET api/v1/banktransactions?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&filter={unallocated|ignored}
    internal static async Task<Results<Ok<List<BankTransactionRowResponse>>, ProblemHttpResult>> GetList(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? filter,
        IQueryHandler<GetBankTransactionsQuery, List<BankTransactionRowResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<BankTransactionRowResponse>> result =
            await handler.Handle(new GetBankTransactionsQuery(dateFrom, dateTo, filter), cancellationToken);
        return result
            .Match<List<BankTransactionRowResponse>, Results<Ok<List<BankTransactionRowResponse>>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/banktransactions/studentcontracts?academicYearId={id}
    internal static async Task<Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>> GetStudentContracts(
        [FromQuery] int academicYearId,
        IQueryHandler<GetBankTransactionStudentContractsQuery, List<LookupItemResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<LookupItemResponse>> result =
            await handler.Handle(new GetBankTransactionStudentContractsQuery(academicYearId), cancellationToken);
        return result.Match<List<LookupItemResponse>, Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/banktransactions/{bankTransactionId:int}
    internal static async Task<Results<Ok<BankTransactionResponse>, ProblemHttpResult>> GetOne(
        [FromRoute] int bankTransactionId, IQueryHandler<GetBankTransactionQuery, BankTransactionResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<BankTransactionResponse> result =
            await handler.Handle(new GetBankTransactionQuery(bankTransactionId), cancellationToken);
        return result.Match<BankTransactionResponse, Results<Ok<BankTransactionResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/banktransactions/import?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd&dryRun={bool}
    internal static async Task<Results<Ok<ImportBankTransactionsResponse>, ProblemHttpResult>> Import(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] bool? dryRun,
        ICommandHandler<ImportBankTransactionsCommand, ImportBankTransactionsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<ImportBankTransactionsResponse> result = await handler.Handle(
            new ImportBankTransactionsCommand(dateFrom, dateTo, dryRun ?? false), cancellationToken);
        return result
            .Match<ImportBankTransactionsResponse, Results<Ok<ImportBankTransactionsResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/banktransactions/{bankTransactionId:int}/allocation
    internal static async Task<Results<Ok, ProblemHttpResult>> SetAllocation([FromRoute] int bankTransactionId,
        [FromBody] BankTransactionAllocationRequest request, ICommandHandler<SetBankTransactionAllocationCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new SetBankTransactionAllocationCommand(bankTransactionId, request),
            cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/banktransactions/{bankTransactionId:int}/ignored
    internal static async Task<Results<Ok, ProblemHttpResult>> SetIgnored([FromRoute] int bankTransactionId,
        [FromBody] BankTransactionIgnoredRequest request, ICommandHandler<SetBankTransactionIgnoredCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new SetBankTransactionIgnoredCommand(bankTransactionId, request.Ignored),
            cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
