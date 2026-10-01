using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.CreateCrmCall;
using AppMimosiGe.Application.CrmCalls.DeleteCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCallFormLookups;
using AppMimosiGe.Application.CrmCalls.GetCrmCallsRowsData;
using AppMimosiGe.Application.CrmCalls.GetCrmCallStudentContracts;
using AppMimosiGe.Application.CrmCalls.UpdateCrmCall;
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

//CRM ზარები მოსწავლეების ოჯახებთან (Access-ის FrmCRMCalls)
public static class CrmCallsEndpoints
{
    public static bool UseCrmCallsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseCrmCallsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.CrmCallsRoute.CrmCallsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveCrmCallsRightsFilter>();

        group.MapGet(Routes.CrmCallsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.CrmCallsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.CrmCallsRoute.StudentContracts, GetStudentContracts);
        group.MapGet(Routes.CrmCallsRoute.GetOne, GetOne);
        group.MapPost(Routes.CrmCallsRoute.Create, Create);
        group.MapPut(Routes.CrmCallsRoute.Update, Update);
        group.MapDelete(Routes.CrmCallsRoute.Delete, Delete);

        debugLogger?.Information("{MethodName} Finished", nameof(UseCrmCallsEndpoints));
        return true;
    }

    // GET api/v1/crmcalls/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<CrmCallsRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest, IQueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<CrmCallsRowsDataResponse> result =
            await handler.Handle(new GetCrmCallsRowsDataQuery(filterSortRequest), cancellationToken);
        return result.Match<CrmCallsRowsDataResponse, Results<Ok<CrmCallsRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/crmcalls/formlookups
    internal static async Task<Results<Ok<CrmCallFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetCrmCallFormLookupsQuery, CrmCallFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<CrmCallFormLookupsResponse> result =
            await handler.Handle(new GetCrmCallFormLookupsQuery(), cancellationToken);
        return result.Match<CrmCallFormLookupsResponse, Results<Ok<CrmCallFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/crmcalls/studentcontracts?academicYearId={id}
    internal static async Task<Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>> GetStudentContracts(
        [FromQuery] int academicYearId,
        IQueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<LookupItemResponse>> result =
            await handler.Handle(new GetCrmCallStudentContractsQuery(academicYearId), cancellationToken);
        return result.Match<List<LookupItemResponse>, Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/crmcalls/{crmCallId:int}
    internal static async Task<Results<Ok<CrmCallResponse>, ProblemHttpResult>> GetOne([FromRoute] int crmCallId,
        IQueryHandler<GetCrmCallQuery, CrmCallResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<CrmCallResponse> result = await handler.Handle(new GetCrmCallQuery(crmCallId), cancellationToken);
        return result.Match<CrmCallResponse, Results<Ok<CrmCallResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/crmcalls
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] CrmCallRequest request,
        ICommandHandler<CreateCrmCallCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateCrmCallCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/crmcalls/{crmCallId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int crmCallId,
        [FromBody] CrmCallRequest request, ICommandHandler<UpdateCrmCallCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateCrmCallCommand(crmCallId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/crmcalls/{crmCallId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int crmCallId,
        ICommandHandler<DeleteCrmCallCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteCrmCallCommand(crmCallId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
