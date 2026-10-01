using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons.GetLesson;
using AppMimosiGe.Application.Lessons.GetLessonFormLookups;
using AppMimosiGe.Application.Lessons.GetLessonsRowsData;
using AppMimosiGe.Application.Lessons.UpdateLesson;
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

//გაკვეთილების ჟურნალი. გაკვეთილებს გენერატორი ქმნის და შლის, ამიტომ აქ შექმნა და წაშლა არ არის (D72)
public static class LessonsEndpoints
{
    public static bool UseLessonsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseLessonsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.LessonsRoute.LessonsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveLessonsRightsFilter>();

        group.MapGet(Routes.LessonsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.LessonsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.LessonsRoute.GetOne, GetOne);
        group.MapPut(Routes.LessonsRoute.Update, Update);

        debugLogger?.Information("{MethodName} Finished", nameof(UseLessonsEndpoints));
        return true;
    }

    // GET api/v1/lessons/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<LessonsRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest, IQueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<LessonsRowsDataResponse> result =
            await handler.Handle(new GetLessonsRowsDataQuery(filterSortRequest), cancellationToken);
        return result.Match<LessonsRowsDataResponse, Results<Ok<LessonsRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/lessons/formlookups
    internal static async Task<Results<Ok<LessonFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<LessonFormLookupsResponse> result =
            await handler.Handle(new GetLessonFormLookupsQuery(), cancellationToken);
        return result.Match<LessonFormLookupsResponse, Results<Ok<LessonFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/lessons/{lessonId:int}
    internal static async Task<Results<Ok<LessonResponse>, ProblemHttpResult>> GetOne([FromRoute] int lessonId,
        IQueryHandler<GetLessonQuery, LessonResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<LessonResponse> result = await handler.Handle(new GetLessonQuery(lessonId), cancellationToken);
        return result.Match<LessonResponse, Results<Ok<LessonResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/lessons/{lessonId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int lessonId,
        [FromBody] LessonRequest request, ICommandHandler<UpdateLessonCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateLessonCommand(lessonId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
