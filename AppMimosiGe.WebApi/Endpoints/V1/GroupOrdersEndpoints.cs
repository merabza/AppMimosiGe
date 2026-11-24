using System;
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

//ჯგუფების ბრძანებები (GROUP_ORDERS.md): მენიუს პუნქტი "ბრძანებები" ხსნის გვერდსაც და endpoint-ებსაც (D184).
//რეჟიმის ვიზარდები ცალკე ჯგუფშია, ადმინისტრატორის უფლებით (GroupOrdersModeRoute)
public static class GroupOrdersEndpoints
{
    public static bool UseGroupOrdersEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseGroupOrdersEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.GroupOrdersRoute.GroupOrdersBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveGroupOrdersRightsFilter>();

        group.MapGet(Routes.GroupOrdersRoute.RowsData, GetRowsData);
        group.MapGet(Routes.GroupOrdersRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.GroupOrdersRoute.Mode, GetMode);
        group.MapGet(Routes.GroupOrdersRoute.GetOne, GetOne);
        group.MapGet(Routes.GroupOrdersRoute.Print, GetPrint);
        group.MapPost(Routes.GroupOrdersRoute.Create, Create);
        group.MapPut(Routes.GroupOrdersRoute.Update, Update);
        group.MapDelete(Routes.GroupOrdersRoute.Delete, Delete);

        debugLogger?.Information("{MethodName} Finished", nameof(UseGroupOrdersEndpoints));
        return true;
    }

    // GET api/v1/grouporders/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<GroupOrdersRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest,
        IQueryHandler<GetGroupOrdersQuery, GroupOrdersRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupOrdersRowsDataResponse> result =
            await handler.Handle(new GetGroupOrdersQuery(filterSortRequest), cancellationToken);
        return result.Match<GroupOrdersRowsDataResponse, Results<Ok<GroupOrdersRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/grouporders/formlookups?academicYearId={id}&orderDate=yyyy-MM-dd&grpId={id}&targetGrpId={id}
    //     &studentStatusId={id}&courseId={id}&groupSizeId={id}&orderId={id}
    internal static async Task<Results<Ok<GroupOrderFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        [FromQuery] int? academicYearId, [FromQuery] DateTime? orderDate, [FromQuery] int? grpId,
        [FromQuery] int? targetGrpId, [FromQuery] int? studentStatusId, [FromQuery] int? courseId,
        [FromQuery] int? groupSizeId, [FromQuery] int? orderId,
        IQueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupOrderFormLookupsResponse> result = await handler.Handle(
            new GetGroupOrderFormLookupsQuery(academicYearId, orderDate, grpId, targetGrpId, studentStatusId,
                courseId, groupSizeId, orderId), cancellationToken);
        return result
            .Match<GroupOrderFormLookupsResponse, Results<Ok<GroupOrderFormLookupsResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/grouporders/mode
    internal static async Task<Results<Ok<GroupOrdersModeResponse>, ProblemHttpResult>> GetMode(
        IQueryHandler<GetGroupOrdersModeQuery, GroupOrdersModeResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupOrdersModeResponse> result = await handler.Handle(new GetGroupOrdersModeQuery(), cancellationToken);
        return result.Match<GroupOrdersModeResponse, Results<Ok<GroupOrdersModeResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/grouporders/{orderId:int}
    internal static async Task<Results<Ok<GroupOrderResponse>, ProblemHttpResult>> GetOne([FromRoute] int orderId,
        IQueryHandler<GetGroupOrderQuery, GroupOrderResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<GroupOrderResponse> result = await handler.Handle(new GetGroupOrderQuery(orderId), cancellationToken);
        return result.Match<GroupOrderResponse, Results<Ok<GroupOrderResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/grouporders/{orderId:int}/print
    internal static async Task<Results<Ok<GroupOrderPrintResponse>, ProblemHttpResult>> GetPrint(
        [FromRoute] int orderId, IQueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupOrderPrintResponse> result =
            await handler.Handle(new GetGroupOrderPrintQuery(orderId), cancellationToken);
        return result.Match<GroupOrderPrintResponse, Results<Ok<GroupOrderPrintResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/grouporders
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] GroupOrderRequest request,
        ICommandHandler<CreateGroupOrderCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateGroupOrderCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/grouporders/{orderId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int orderId,
        [FromBody] GroupOrderRequest request, ICommandHandler<UpdateGroupOrderCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateGroupOrderCommand(orderId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/grouporders/{orderId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int orderId,
        ICommandHandler<DeleteGroupOrderCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteGroupOrderCommand(orderId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
