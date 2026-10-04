using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.CountSalary;
using AppMimosiGe.Application.Salary.CreateSalaryHeader;
using AppMimosiGe.Application.Salary.CreateSalaryPart;
using AppMimosiGe.Application.Salary.DeleteSalaryHeader;
using AppMimosiGe.Application.Salary.DeleteSalaryPart;
using AppMimosiGe.Application.Salary.GetDeclarationFile;
using AppMimosiGe.Application.Salary.GetSalaryFormLookups;
using AppMimosiGe.Application.Salary.GetSalaryHeader;
using AppMimosiGe.Application.Salary.GetSalaryHeaders;
using AppMimosiGe.Application.Salary.GetTransferFile;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGe.Application.Salary.UpdateSalaryHeader;
using AppMimosiGe.Application.Salary.UpdateSalaryPart;
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

//ხელფასის უწყისები, მდგენელები, გამოთვლა და ფაილები (Access-ის FrmSalary)
public static class SalaryEndpoints
{
    private const string CsvContentType = "text/csv";

    public static bool UseSalaryEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseSalaryEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.SalaryRoute.SalaryBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHaveSalaryRightsFilter>();

        group.MapGet(Routes.SalaryRoute.Headers, GetHeaders);
        group.MapGet(Routes.SalaryRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.SalaryRoute.GetOne, GetOne);
        group.MapPost(Routes.SalaryRoute.Create, Create);
        group.MapPut(Routes.SalaryRoute.Update, Update);
        group.MapDelete(Routes.SalaryRoute.Delete, Delete);
        group.MapPost(Routes.SalaryRoute.CreatePart, CreatePart);
        group.MapPut(Routes.SalaryRoute.UpdatePart, UpdatePart);
        group.MapDelete(Routes.SalaryRoute.DeletePart, DeletePart);
        group.MapPost(Routes.SalaryRoute.Count, Count);
        group.MapGet(Routes.SalaryRoute.TransferFile, TransferFile);
        group.MapGet(Routes.SalaryRoute.DeclarationFile, DeclarationFile);

        debugLogger?.Information("{MethodName} Finished", nameof(UseSalaryEndpoints));
        return true;
    }

    // GET api/v1/salary/headers
    internal static async Task<Results<Ok<List<SalaryHeaderRowResponse>>, ProblemHttpResult>> GetHeaders(
        IQueryHandler<GetSalaryHeadersQuery, List<SalaryHeaderRowResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<SalaryHeaderRowResponse>> result =
            await handler.Handle(new GetSalaryHeadersQuery(), cancellationToken);
        return result
            .Match<List<SalaryHeaderRowResponse>, Results<Ok<List<SalaryHeaderRowResponse>>, ProblemHttpResult>>(
                success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/salary/formlookups
    internal static async Task<Results<Ok<SalaryFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetSalaryFormLookupsQuery, SalaryFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<SalaryFormLookupsResponse> result =
            await handler.Handle(new GetSalaryFormLookupsQuery(), cancellationToken);
        return result.Match<SalaryFormLookupsResponse, Results<Ok<SalaryFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/salary/{shId:int}
    internal static async Task<Results<Ok<SalaryHeaderResponse>, ProblemHttpResult>> GetOne([FromRoute] int shId,
        IQueryHandler<GetSalaryHeaderQuery, SalaryHeaderResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<SalaryHeaderResponse> result = await handler.Handle(new GetSalaryHeaderQuery(shId), cancellationToken);
        return result.Match<SalaryHeaderResponse, Results<Ok<SalaryHeaderResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/salary
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] SalaryHeaderRequest request,
        ICommandHandler<CreateSalaryHeaderCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateSalaryHeaderCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/salary/{shId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int shId,
        [FromBody] SalaryHeaderRequest request, ICommandHandler<UpdateSalaryHeaderCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateSalaryHeaderCommand(shId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/salary/{shId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int shId,
        ICommandHandler<DeleteSalaryHeaderCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteSalaryHeaderCommand(shId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/salary/{shId:int}/parts
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> CreatePart([FromRoute] int shId,
        [FromBody] SalaryPartRequest request, ICommandHandler<CreateSalaryPartCommand, int> handler,
        CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreateSalaryPartCommand(shId, request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/salary/parts/{spId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> UpdatePart([FromRoute] int spId,
        [FromBody] SalaryPartRequest request, ICommandHandler<UpdateSalaryPartCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdateSalaryPartCommand(spId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/salary/parts/{spId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> DeletePart([FromRoute] int spId,
        ICommandHandler<DeleteSalaryPartCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeleteSalaryPartCommand(spId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/salary/{shId:int}/count
    internal static async Task<Results<Ok<SalaryCountResponse>, ProblemHttpResult>> Count([FromRoute] int shId,
        ICommandHandler<CountSalaryCommand, SalaryCountResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<SalaryCountResponse> result = await handler.Handle(new CountSalaryCommand(shId), cancellationToken);
        return result.Match<SalaryCountResponse, Results<Ok<SalaryCountResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/salary/{shId:int}/transferfile
    internal static async Task<Results<FileContentHttpResult, ProblemHttpResult>> TransferFile([FromRoute] int shId,
        IQueryHandler<GetTransferFileQuery, SalaryFile> handler, CancellationToken cancellationToken = default)
    {
        Result<SalaryFile> result = await handler.Handle(new GetTransferFileQuery(shId), cancellationToken);
        return result.Match<SalaryFile, Results<FileContentHttpResult, ProblemHttpResult>>(
            success => TypedResults.File(success.Content, CsvContentType, success.FileName),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/salary/declarationfile?month=yyyy-MM-dd
    internal static async Task<Results<FileContentHttpResult, ProblemHttpResult>> DeclarationFile(
        [FromQuery] DateTime? month, IQueryHandler<GetDeclarationFileQuery, SalaryFile> handler,
        CancellationToken cancellationToken = default)
    {
        Result<SalaryFile> result = await handler.Handle(new GetDeclarationFileQuery(month), cancellationToken);
        return result.Match<SalaryFile, Results<FileContentHttpResult, ProblemHttpResult>>(
            success => TypedResults.File(success.Content, CsvContentType, success.FileName),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
