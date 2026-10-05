using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;
using AppMimosiGe.Application.AcademicYears.CreateAcademicYear;
using AppMimosiGe.Application.AcademicYears.GetAcademicYears;
using AppMimosiGe.Application.AcademicYears.GetAcademicYearWizardInfo;
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

//„ახალი სასწავლო წლის" ოსტატი (ნაწილი 20): ყოველი ნაბიჯი ცალკე ბრძანებაა, dryRun-ით
public static class AcademicYearEndpoints
{
    public static bool UseAcademicYearEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseAcademicYearEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.AcademicYearRoute.AcademicYearBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveNewAcademicYearRightsFilter>();

        group.MapGet(Routes.AcademicYearRoute.Info, GetInfo);
        group.MapPost(Routes.AcademicYearRoute.Create, Create);
        group.MapPost(Routes.AcademicYearRoute.CloseGroups, CloseGroups);

        //წლების სია გლობალური წლის ამომრჩევისთვისაა (ყველა გვერდზე), ამიტომ მხოლოდ შესვლას ითხოვს
        endpoints.MapGroup(Routes.ApiBase + Routes.AcademicYearsRoute.AcademicYearsBase).RequireAuthorization()
            .MapGet(Routes.AcademicYearsRoute.List, GetAcademicYears);

        debugLogger?.Information("{MethodName} Finished", nameof(UseAcademicYearEndpoints));
        return true;
    }

    // GET api/v1/academicyears
    internal static async Task<Results<Ok<AcademicYearsResponse>, ProblemHttpResult>> GetAcademicYears(
        IQueryHandler<GetAcademicYearsQuery, AcademicYearsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<AcademicYearsResponse> result = await handler.Handle(new GetAcademicYearsQuery(), cancellationToken);
        return result.Match<AcademicYearsResponse, Results<Ok<AcademicYearsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/academicyear/info
    internal static async Task<Results<Ok<AcademicYearWizardInfoResponse>, ProblemHttpResult>> GetInfo(
        IQueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<AcademicYearWizardInfoResponse> result =
            await handler.Handle(new GetAcademicYearWizardInfoQuery(), cancellationToken);
        return result
            .Match<AcademicYearWizardInfoResponse, Results<Ok<AcademicYearWizardInfoResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/academicyear/create?dryRun={bool}
    internal static async Task<Results<Ok<NewAcademicYearResponse>, ProblemHttpResult>> Create(
        ICommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse> handler, [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        Result<NewAcademicYearResponse> result =
            await handler.Handle(new CreateAcademicYearCommand(dryRun), cancellationToken);
        return result.Match<NewAcademicYearResponse, Results<Ok<NewAcademicYearResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/academicyear/{ayId:int}/closegroups?closeDate=yyyy-MM-dd&dryRun={bool}
    internal static async Task<Results<Ok<CloseAcademicYearGroupsResponse>, ProblemHttpResult>> CloseGroups(
        [FromRoute] int ayId,
        ICommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse> handler,
        [FromQuery] DateTime? closeDate = null, [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        Result<CloseAcademicYearGroupsResponse> result =
            await handler.Handle(new CloseAcademicYearGroupsCommand(ayId, closeDate, dryRun), cancellationToken);
        return result
            .Match<CloseAcademicYearGroupsResponse, Results<Ok<CloseAcademicYearGroupsResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
