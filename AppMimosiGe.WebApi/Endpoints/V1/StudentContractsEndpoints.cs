using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.CreateStudentContract;
using AppMimosiGe.Application.StudentContracts.DeleteStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;
using AppMimosiGe.Application.StudentContracts.GetStudentContractNextNumber;
using AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.StudentContracts.UpdateStudentContract;
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

public static class StudentContractsEndpoints
{
    public static bool UseStudentContractsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseStudentContractsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.StudentContractsRoute.StudentContractsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveStudentContractsRightsFilter>();

        group.MapGet(Routes.StudentContractsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.StudentContractsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.StudentContractsRoute.Humans, SearchHumans);
        group.MapGet(Routes.StudentContractsRoute.NextNumber, GetNextNumber);
        group.MapGet(Routes.StudentContractsRoute.GetOne, GetOne);
        group.MapPost(Routes.StudentContractsRoute.Create, Create);
        group.MapPut(Routes.StudentContractsRoute.Update, Update);
        group.MapDelete(Routes.StudentContractsRoute.Delete, Delete);

        debugLogger?.Information("{MethodName} Finished", nameof(UseStudentContractsEndpoints));
        return true;
    }

    // GET api/v1/studentcontracts/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<StudentContractsRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest,
        IQueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<StudentContractsRowsDataResponse> result =
            await handler.Handle(new GetStudentContractsRowsDataQuery(filterSortRequest), cancellationToken);
        return result
            .Match<StudentContractsRowsDataResponse, Results<Ok<StudentContractsRowsDataResponse>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/studentcontracts/formlookups
    internal static async Task<Results<Ok<StudentContractFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<StudentContractFormLookupsResponse> result =
            await handler.Handle(new GetStudentContractFormLookupsQuery(), cancellationToken);
        return result
            .Match<StudentContractFormLookupsResponse,
                Results<Ok<StudentContractFormLookupsResponse>, ProblemHttpResult>>(success => TypedResults.Ok(success),
                failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/studentcontracts/humans?search={text}
    internal static async Task<Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>> SearchHumans(
        [FromQuery] string? search, IQueryHandler<SearchHumansQuery, List<LookupItemResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<LookupItemResponse>>
            result = await handler.Handle(new SearchHumansQuery(search), cancellationToken);
        return result.Match<List<LookupItemResponse>, Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/studentcontracts/nextnumber?academicYearId={id}
    internal static async Task<Results<Ok<StudentContractNextNumberResponse>, ProblemHttpResult>> GetNextNumber(
        [FromQuery] int academicYearId,
        IQueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<StudentContractNextNumberResponse> result =
            await handler.Handle(new GetStudentContractNextNumberQuery(academicYearId), cancellationToken);
        return result
            .Match<StudentContractNextNumberResponse,
                Results<Ok<StudentContractNextNumberResponse>, ProblemHttpResult>>(success => TypedResults.Ok(success),
                failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/studentcontracts/{scId:int}
    internal static async Task<Results<Ok<StudentContractResponse>, ProblemHttpResult>> GetOne([FromRoute] int scId,
        IQueryHandler<GetStudentContractQuery, StudentContractResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<StudentContractResponse> result =
            await handler.Handle(new GetStudentContractQuery(scId), cancellationToken);
        return result.Match<StudentContractResponse, Results<Ok<StudentContractResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/studentcontracts
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] StudentContractRequest request,
        ICommandHandler<CreateStudentContractCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateStudentContractCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/studentcontracts/{scId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int scId,
        [FromBody] StudentContractRequest request, ICommandHandler<UpdateStudentContractCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateStudentContractCommand(scId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/studentcontracts/{scId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int scId,
        ICommandHandler<DeleteStudentContractCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteStudentContractCommand(scId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
