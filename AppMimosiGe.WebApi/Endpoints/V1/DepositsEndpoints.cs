using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.GetBalancesFormLookups;
using AppMimosiGe.Application.Balances.GetDeposits;
using AppMimosiGe.Application.Balances.RecountBalances;
using AppMimosiGe.WebApi.Filters;
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

//ბალანსები (Access-ის FrmDeposites). გახსნისას გვერდი ჯერ dirty ჯგუფებსა და კონტრაქტებს გადაითვლის. სრული
//გადაანგარიშება (ყველა ჯგუფი და კონტრაქტი) ყველა ჯგუფის გაკვეთილების გადაანგარიშების სპეციალურ უფლებას ითხოვს:
//Access-ში ფარული ღილაკი იყო
public static class DepositsEndpoints
{
    public static bool UseDepositsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseDepositsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.DepositsRoute.DepositsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveDepositsRightsFilter>();

        group.MapGet(Routes.DepositsRoute.Rows, GetRows);
        group.MapGet(Routes.DepositsRoute.FormLookups, GetFormLookups);
        group.MapPost(Routes.DepositsRoute.Recount, Recount);
        group.MapPost(Routes.DepositsRoute.FullRecount, FullRecount)
            .AddEndpointFilter<UserMustHaveRecountAllLessonsRightFilter>();

        debugLogger?.Information("{MethodName} Finished", nameof(UseDepositsEndpoints));
        return true;
    }

    // GET api/v1/deposits/rows?academicYearId={id}&maximum={number}&dateTo={yyyy-MM-dd}&filter={filter|call}
    internal static async Task<Results<Ok<DepositsResponse>, ProblemHttpResult>> GetRows([FromQuery] DateTime dateTo,
        IQueryHandler<GetDepositsQuery, DepositsResponse> handler, [FromQuery] int? academicYearId = null,
        [FromQuery] decimal maximum = 0m, [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        Result<DepositsResponse> result =
            await handler.Handle(new GetDepositsQuery(academicYearId, maximum, dateTo, filter), cancellationToken);
        return result.Match<DepositsResponse, Results<Ok<DepositsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/deposits/formlookups
    internal static async Task<Results<Ok<BalancesFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<BalancesFormLookupsResponse> result =
            await handler.Handle(new GetBalancesFormLookupsQuery(), cancellationToken);
        return result.Match<BalancesFormLookupsResponse, Results<Ok<BalancesFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/deposits/recount
    internal static Task<Results<Ok<BalancesRecountResponse>, ProblemHttpResult>> Recount(
        ICommandHandler<RecountBalancesCommand, BalancesRecountResponse> handler,
        CancellationToken cancellationToken = default)
    {
        return RunRecount(handler, true, cancellationToken);
    }

    // POST api/v1/deposits/fullrecount
    internal static Task<Results<Ok<BalancesRecountResponse>, ProblemHttpResult>> FullRecount(
        ICommandHandler<RecountBalancesCommand, BalancesRecountResponse> handler,
        CancellationToken cancellationToken = default)
    {
        return RunRecount(handler, false, cancellationToken);
    }

    private static async Task<Results<Ok<BalancesRecountResponse>, ProblemHttpResult>> RunRecount(
        ICommandHandler<RecountBalancesCommand, BalancesRecountResponse> handler, bool onlyDirty,
        CancellationToken cancellationToken)
    {
        Result<BalancesRecountResponse> result =
            await handler.Handle(new RecountBalancesCommand(onlyDirty), cancellationToken);
        return result.Match<BalancesRecountResponse, Results<Ok<BalancesRecountResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
