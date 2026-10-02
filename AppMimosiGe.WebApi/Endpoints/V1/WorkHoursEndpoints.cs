using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;
using AppMimosiGe.Application.WorkHours.CreateWorkHour;
using AppMimosiGe.Application.WorkHours.DeleteWorkHour;
using AppMimosiGe.Application.WorkHours.EndWork;
using AppMimosiGe.Application.WorkHours.GetWorkHour;
using AppMimosiGe.Application.WorkHours.GetWorkHourFormLookups;
using AppMimosiGe.Application.WorkHours.GetWorkHoursRowsData;
using AppMimosiGe.Application.WorkHours.StartWork;
using AppMimosiGe.Application.WorkHours.UpdateWorkHour;
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

//თანამშრომლების ნამუშევარი დრო (Access-ის FrmWorkHours)
public static class WorkHoursEndpoints
{
    public static bool UseWorkHoursEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseWorkHoursEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.WorkHoursRoute.WorkHoursBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveWorkHoursRightsFilter>();

        group.MapGet(Routes.WorkHoursRoute.RowsData, GetRowsData);
        group.MapGet(Routes.WorkHoursRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.WorkHoursRoute.GetOne, GetOne);
        group.MapPost(Routes.WorkHoursRoute.Create, Create);
        group.MapPut(Routes.WorkHoursRoute.Update, Update);
        group.MapDelete(Routes.WorkHoursRoute.Delete, Delete);
        group.MapPost(Routes.WorkHoursRoute.Start, Start);
        group.MapPost(Routes.WorkHoursRoute.End, End);
        group.MapPost(Routes.WorkHoursRoute.AutoGenerate, AutoGenerate);

        debugLogger?.Information("{MethodName} Finished", nameof(UseWorkHoursEndpoints));
        return true;
    }

    // GET api/v1/workhours/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<WorkHoursRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest,
        IQueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<WorkHoursRowsDataResponse> result =
            await handler.Handle(new GetWorkHoursRowsDataQuery(filterSortRequest), cancellationToken);
        return result.Match<WorkHoursRowsDataResponse, Results<Ok<WorkHoursRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/workhours/formlookups
    internal static async Task<Results<Ok<WorkHourFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<WorkHourFormLookupsResponse> result =
            await handler.Handle(new GetWorkHourFormLookupsQuery(), cancellationToken);
        return result.Match<WorkHourFormLookupsResponse, Results<Ok<WorkHourFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/workhours/{whId:int}
    internal static async Task<Results<Ok<WorkHourResponse>, ProblemHttpResult>> GetOne([FromRoute] int whId,
        IQueryHandler<GetWorkHourQuery, WorkHourResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<WorkHourResponse> result = await handler.Handle(new GetWorkHourQuery(whId), cancellationToken);
        return result.Match<WorkHourResponse, Results<Ok<WorkHourResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/workhours
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] WorkHourRequest request,
        ICommandHandler<CreateWorkHourCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateWorkHourCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/workhours/{whId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int whId,
        [FromBody] WorkHourRequest request, ICommandHandler<UpdateWorkHourCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateWorkHourCommand(whId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/workhours/{whId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int whId,
        ICommandHandler<DeleteWorkHourCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteWorkHourCommand(whId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/workhours/start
    internal static async Task<Results<Ok<WorkHourResponse>, ProblemHttpResult>> Start(
        [FromBody] WorkTimeFixRequest request, ICommandHandler<StartWorkCommand, WorkHourResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<WorkHourResponse> result = await handler.Handle(new StartWorkCommand(request), cancellationToken);
        return result.Match<WorkHourResponse, Results<Ok<WorkHourResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/workhours/end
    internal static async Task<Results<Ok<WorkHourResponse>, ProblemHttpResult>> End(
        [FromBody] WorkTimeFixRequest request, ICommandHandler<EndWorkCommand, WorkHourResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<WorkHourResponse> result = await handler.Handle(new EndWorkCommand(request), cancellationToken);
        return result.Match<WorkHourResponse, Results<Ok<WorkHourResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/workhours/autogenerate
    internal static async Task<Results<Ok<WorkHoursAutoGenerateResponse>, ProblemHttpResult>> AutoGenerate(
        [FromBody] WorkHoursAutoGenerateRequest request,
        ICommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<WorkHoursAutoGenerateResponse> result =
            await handler.Handle(new AutoGenerateWorkHoursCommand(request), cancellationToken);
        return result
            .Match<WorkHoursAutoGenerateResponse, Results<Ok<WorkHoursAutoGenerateResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
