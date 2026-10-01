using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.GetBalancesFormLookups;
using AppMimosiGe.Application.Balances.GetStatement;
using AppMimosiGe.Application.Balances.GetStatementStudentContracts;
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

//დარიცხვები და გადახდები: ამონაწერი (Access-ის FrmChargesAndPayments)
public static class ChargesAndPaymentsEndpoints
{
    public static bool UseChargesAndPaymentsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseChargesAndPaymentsEndpoints));

        RouteGroupBuilder group = endpoints
            .MapGroup(Routes.ApiBase + Routes.ChargesAndPaymentsRoute.ChargesAndPaymentsBase).RequireAuthorization()
            .AddEndpointFilter<UserMustHaveChargesAndPaymentsRightsFilter>();

        group.MapGet(Routes.ChargesAndPaymentsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.ChargesAndPaymentsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.ChargesAndPaymentsRoute.StudentContracts, GetStudentContracts);

        debugLogger?.Information("{MethodName} Finished", nameof(UseChargesAndPaymentsEndpoints));
        return true;
    }

    // GET api/v1/chargesandpayments/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<StatementRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest, IQueryHandler<GetStatementQuery, StatementRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<StatementRowsDataResponse> result =
            await handler.Handle(new GetStatementQuery(filterSortRequest), cancellationToken);
        return result.Match<StatementRowsDataResponse, Results<Ok<StatementRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/chargesandpayments/formlookups
    internal static async Task<Results<Ok<BalancesFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<BalancesFormLookupsResponse> result =
            await handler.Handle(new GetBalancesFormLookupsQuery(), cancellationToken);
        return result.Match<BalancesFormLookupsResponse, Results<Ok<BalancesFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/chargesandpayments/studentcontracts?academicYearId={id}
    internal static async Task<Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>> GetStudentContracts(
        [FromQuery] int academicYearId,
        IQueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<LookupItemResponse>> result =
            await handler.Handle(new GetStatementStudentContractsQuery(academicYearId), cancellationToken);
        return result.Match<List<LookupItemResponse>, Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
