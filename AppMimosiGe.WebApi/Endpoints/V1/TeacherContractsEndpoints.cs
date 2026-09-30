using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;
using AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;
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

public static class TeacherContractsEndpoints
{
    public static bool UseTeacherContractsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseTeacherContractsEndpoints));

        RouteGroupBuilder group = endpoints
            .MapGroup(Routes.ApiBase + Routes.TeacherContractsRoute.TeacherContractsBase).RequireAuthorization()
            .AddEndpointFilter<UserMustHaveTeacherContractsRightsFilter>();

        group.MapGet(Routes.TeacherContractsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.TeacherContractsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.TeacherContractsRoute.Humans, SearchHumans);
        group.MapGet(Routes.TeacherContractsRoute.GetOne, GetOne);
        group.MapPost(Routes.TeacherContractsRoute.Create, Create);
        group.MapPut(Routes.TeacherContractsRoute.Update, Update);
        group.MapDelete(Routes.TeacherContractsRoute.Delete, Delete);

        debugLogger?.Information("{MethodName} Finished", nameof(UseTeacherContractsEndpoints));
        return true;
    }

    // GET api/v1/teachercontracts/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<TeacherContractsRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest,
        IQueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<TeacherContractsRowsDataResponse> result =
            await handler.Handle(new GetTeacherContractsRowsDataQuery(filterSortRequest), cancellationToken);
        return result.Match<TeacherContractsRowsDataResponse,
            Results<Ok<TeacherContractsRowsDataResponse>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/teachercontracts/formlookups
    internal static async Task<Results<Ok<TeacherContractFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<TeacherContractFormLookupsResponse> result =
            await handler.Handle(new GetTeacherContractFormLookupsQuery(), cancellationToken);
        return result.Match<TeacherContractFormLookupsResponse,
            Results<Ok<TeacherContractFormLookupsResponse>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/teachercontracts/humans?search={text}
    //იგივე ძებნაა, რაც მოსწავლეების კონტრაქტებში, ოღონდ ამ გვერდის უფლებით
    internal static async Task<Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>> SearchHumans(
        [FromQuery] string? search, IQueryHandler<SearchHumansQuery, List<LookupItemResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<LookupItemResponse>> result = await handler.Handle(new SearchHumansQuery(search), cancellationToken);
        return result.Match<List<LookupItemResponse>, Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/teachercontracts/{id:int}
    internal static async Task<Results<Ok<TeacherContractResponse>, ProblemHttpResult>> GetOne([FromRoute] int id,
        IQueryHandler<GetTeacherContractQuery, TeacherContractResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<TeacherContractResponse> result = await handler.Handle(new GetTeacherContractQuery(id), cancellationToken);
        return result.Match<TeacherContractResponse, Results<Ok<TeacherContractResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/teachercontracts
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] TeacherContractRequest request,
        ICommandHandler<CreateTeacherContractCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateTeacherContractCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/teachercontracts/{id:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int id,
        [FromBody] TeacherContractRequest request, ICommandHandler<UpdateTeacherContractCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateTeacherContractCommand(id, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/teachercontracts/{id:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int id,
        ICommandHandler<DeleteTeacherContractCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteTeacherContractCommand(id), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
