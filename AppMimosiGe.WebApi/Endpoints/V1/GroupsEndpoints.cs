using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.CreateGroup;
using AppMimosiGe.Application.Groups.DeleteGroup;
using AppMimosiGe.Application.Groups.GetGroup;
using AppMimosiGe.Application.Groups.GetGroupFormLookups;
using AppMimosiGe.Application.Groups.GetGroupsRowsData;
using AppMimosiGe.Application.Groups.GetGroupStudentContracts;
using AppMimosiGe.Application.Groups.UpdateGroup;
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

public static class GroupsEndpoints
{
    public static bool UseGroupsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseGroupsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.GroupsRoute.GroupsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveGroupsRightsFilter>();

        group.MapGet(Routes.GroupsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.GroupsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.GroupsRoute.StudentContracts, GetStudentContracts);
        group.MapGet(Routes.GroupsRoute.GetOne, GetOne);
        group.MapPost(Routes.GroupsRoute.Create, Create);
        group.MapPut(Routes.GroupsRoute.Update, Update);
        group.MapDelete(Routes.GroupsRoute.Delete, Delete);

        debugLogger?.Information("{MethodName} Finished", nameof(UseGroupsEndpoints));
        return true;
    }

    // GET api/v1/groups/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<GroupsRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest, IQueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupsRowsDataResponse> result =
            await handler.Handle(new GetGroupsRowsDataQuery(filterSortRequest), cancellationToken);
        return result.Match<GroupsRowsDataResponse, Results<Ok<GroupsRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/groups/formlookups
    internal static async Task<Results<Ok<GroupFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupFormLookupsResponse> result =
            await handler.Handle(new GetGroupFormLookupsQuery(), cancellationToken);
        return result.Match<GroupFormLookupsResponse, Results<Ok<GroupFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/groups/studentcontracts?academicYearId={id}
    internal static async Task<Results<Ok<List<GroupStudentContractLookupResponse>>, ProblemHttpResult>>
        GetStudentContracts([FromQuery] int academicYearId,
            IQueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>> handler,
            CancellationToken cancellationToken = default)
    {
        Result<List<GroupStudentContractLookupResponse>> result =
            await handler.Handle(new GetGroupStudentContractsQuery(academicYearId), cancellationToken);
        return result
            .Match<List<GroupStudentContractLookupResponse>,
                Results<Ok<List<GroupStudentContractLookupResponse>>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/groups/{grpId:int}
    internal static async Task<Results<Ok<GroupResponse>, ProblemHttpResult>> GetOne([FromRoute] int grpId,
        IQueryHandler<GetGroupQuery, GroupResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<GroupResponse> result = await handler.Handle(new GetGroupQuery(grpId), cancellationToken);
        return result.Match<GroupResponse, Results<Ok<GroupResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/groups
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] GroupRequest request,
        ICommandHandler<CreateGroupCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateGroupCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/groups/{grpId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int grpId,
        [FromBody] GroupRequest request, ICommandHandler<UpdateGroupCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateGroupCommand(grpId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/groups/{grpId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int grpId,
        ICommandHandler<DeleteGroupCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteGroupCommand(grpId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
