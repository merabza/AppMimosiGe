using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLastLesson;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLessons;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;
using AppMimosiGe.Application.LessonGenerator.GetLessonGeneratorLog;
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

//გაკვეთილების გენერატორი ჯგუფების გვერდიდან ეშვება, ამიტომ ჯგუფების მენიუს პუნქტის უფლებას ამოწმებს. ყველა ჯგუფის
//გადაანგარიშებას სპეციალური უფლებაც სჭირდება
public static class LessonGeneratorEndpoints
{
    public static bool UseLessonGeneratorEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseLessonGeneratorEndpoints));

        RouteGroupBuilder group = endpoints
            .MapGroup(Routes.ApiBase + Routes.LessonGeneratorRoute.LessonGeneratorBase).RequireAuthorization()
            .AddEndpointFilter<UserMustHaveGroupsRightsFilter>();

        group.MapPost(Routes.LessonGeneratorRoute.GroupLessons, GenerateGroupLessons);
        group.MapPost(Routes.LessonGeneratorRoute.GroupLastLesson, GenerateGroupLastLesson);
        group.MapPost(Routes.LessonGeneratorRoute.DirtyGroups, GenerateDirtyGroupsLessons);
        group.MapPost(Routes.LessonGeneratorRoute.AllGroups, GenerateAllGroupsLessons)
            .AddEndpointFilter<UserMustHaveRecountAllLessonsRightFilter>();
        group.MapGet(Routes.LessonGeneratorRoute.Log, GetLog);

        debugLogger?.Information("{MethodName} Finished", nameof(UseLessonGeneratorEndpoints));
        return true;
    }

    // POST api/v1/lessongenerator/groups/{grpId:int}?dryRun={bool}
    internal static async Task<Results<Ok<LessonsGenerationResponse>, ProblemHttpResult>> GenerateGroupLessons(
        [FromRoute] int grpId, ICommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse> handler,
        [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
    {
        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupLessonsCommand(grpId, dryRun), cancellationToken);
        return ToHttpResult(result);
    }

    // POST api/v1/lessongenerator/groups/{grpId:int}/lastlesson
    internal static async Task<Results<Ok<GroupLastLessonResponse>, ProblemHttpResult>> GenerateGroupLastLesson(
        [FromRoute] int grpId, ICommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<GroupLastLessonResponse> result =
            await handler.Handle(new GenerateGroupLastLessonCommand(grpId), cancellationToken);
        return result.Match<GroupLastLessonResponse, Results<Ok<GroupLastLessonResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/lessongenerator/dirtygroups?dryRun={bool}
    internal static async Task<Results<Ok<LessonsGenerationResponse>, ProblemHttpResult>> GenerateDirtyGroupsLessons(
        ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse> handler, [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(true, dryRun), cancellationToken);
        return ToHttpResult(result);
    }

    // POST api/v1/lessongenerator/allgroups?dryRun={bool}
    internal static async Task<Results<Ok<LessonsGenerationResponse>, ProblemHttpResult>> GenerateAllGroupsLessons(
        ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse> handler, [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(false, dryRun), cancellationToken);
        return ToHttpResult(result);
    }

    // GET api/v1/lessongenerator/log?grpId={id}
    internal static async Task<Results<Ok<List<LessonGeneratorLogRowResponse>>, ProblemHttpResult>> GetLog(
        IQueryHandler<GetLessonGeneratorLogQuery, List<LessonGeneratorLogRowResponse>> handler,
        [FromQuery] int? grpId = null, CancellationToken cancellationToken = default)
    {
        Result<List<LessonGeneratorLogRowResponse>> result =
            await handler.Handle(new GetLessonGeneratorLogQuery(grpId), cancellationToken);
        return result
            .Match<List<LessonGeneratorLogRowResponse>,
                Results<Ok<List<LessonGeneratorLogRowResponse>>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    private static Results<Ok<LessonsGenerationResponse>, ProblemHttpResult> ToHttpResult(
        Result<LessonsGenerationResponse> result)
    {
        return result.Match<LessonsGenerationResponse, Results<Ok<LessonsGenerationResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
