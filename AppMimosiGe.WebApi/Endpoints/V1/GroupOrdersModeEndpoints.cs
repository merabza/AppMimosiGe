using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.GetGroupOrdersExportFile;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupEditing;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;
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

//ბრძანებების რეჟიმის ვიზარდები (D194, D195): მენიუს პუნქტი "ბრძანებების რეჟიმი", მხოლოდ Admin-ისთვის (D184).
//რეჟიმის წაკითხვა (GET grouporders/mode) ბრძანებების უფლებითაა (GroupOrdersEndpoints)
public static class GroupOrdersModeEndpoints
{
    public static bool UseGroupOrdersModeEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseGroupOrdersModeEndpoints));

        RouteGroupBuilder group = endpoints
            .MapGroup(Routes.ApiBase + Routes.GroupOrdersModeRoute.GroupOrdersModeBase).RequireAuthorization()
            .AddEndpointFilter<UserMustHaveGroupOrdersModeRightsFilter>();

        group.MapPost(Routes.GroupOrdersModeRoute.SwitchToGroupOrders, SwitchToGroupOrders);
        group.MapPost(Routes.GroupOrdersModeRoute.SwitchToGroupEditing, SwitchToGroupEditing);
        group.MapGet(Routes.GroupOrdersModeRoute.ExportFile, ExportFile);

        debugLogger?.Information("{MethodName} Finished", nameof(UseGroupOrdersModeEndpoints));
        return true;
    }

    // POST api/v1/grouporders/mode/toorders?dryRun={bool}
    internal static async Task<Results<Ok<SwitchToGroupOrdersResponse>, ProblemHttpResult>> SwitchToGroupOrders(
        ICommandHandler<SwitchToGroupOrdersCommand, SwitchToGroupOrdersResponse> handler,
        [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
    {
        Result<SwitchToGroupOrdersResponse> result =
            await handler.Handle(new SwitchToGroupOrdersCommand(dryRun), cancellationToken);
        return result.Match<SwitchToGroupOrdersResponse, Results<Ok<SwitchToGroupOrdersResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/grouporders/mode/togroups
    internal static async Task<Results<Ok<SwitchToGroupEditingResponse>, ProblemHttpResult>> SwitchToGroupEditing(
        ICommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<SwitchToGroupEditingResponse> result =
            await handler.Handle(new SwitchToGroupEditingCommand(), cancellationToken);
        return result
            .Match<SwitchToGroupEditingResponse, Results<Ok<SwitchToGroupEditingResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/grouporders/mode/files/{fileName}
    internal static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ExportFile(
        [FromRoute] string fileName, IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupOrdersExportFile> result =
            await handler.Handle(new GetGroupOrdersExportFileQuery(fileName), cancellationToken);
        return result.Match<GroupOrdersExportFile, Results<FileContentHttpResult, ProblemHttpResult>>(
            success => TypedResults.File(success.Content, GroupOrdersExportFile.ContentType, success.FileName),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
