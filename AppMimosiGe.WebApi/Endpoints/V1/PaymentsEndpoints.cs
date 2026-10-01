using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments.CreatePayment;
using AppMimosiGe.Application.Payments.DeletePayment;
using AppMimosiGe.Application.Payments.GetPayment;
using AppMimosiGe.Application.Payments.GetPaymentFormLookups;
using AppMimosiGe.Application.Payments.GetPaymentsRowsData;
using AppMimosiGe.Application.Payments.GetPaymentStudentContracts;
using AppMimosiGe.Application.Payments.UpdatePayment;
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

//მოსწავლეების გადახდები (Access-ის FrmPayments)
public static class PaymentsEndpoints
{
    public static bool UsePaymentsEndpoints(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UsePaymentsEndpoints));

        RouteGroupBuilder group = endpoints.MapGroup(Routes.ApiBase + Routes.PaymentsRoute.PaymentsBase)
            .RequireAuthorization().AddEndpointFilter<UserMustHavePaymentsRightsFilter>();

        group.MapGet(Routes.PaymentsRoute.RowsData, GetRowsData);
        group.MapGet(Routes.PaymentsRoute.FormLookups, GetFormLookups);
        group.MapGet(Routes.PaymentsRoute.StudentContracts, GetStudentContracts);
        group.MapGet(Routes.PaymentsRoute.GetOne, GetOne);
        group.MapPost(Routes.PaymentsRoute.Create, Create);
        group.MapPut(Routes.PaymentsRoute.Update, Update);
        group.MapDelete(Routes.PaymentsRoute.Delete, Delete);

        debugLogger?.Information("{MethodName} Finished", nameof(UsePaymentsEndpoints));
        return true;
    }

    // GET api/v1/payments/rowsdata?filterSortRequest={base64}
    internal static async Task<Results<Ok<PaymentsRowsDataResponse>, ProblemHttpResult>> GetRowsData(
        [FromQuery] string filterSortRequest,
        IQueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<PaymentsRowsDataResponse> result =
            await handler.Handle(new GetPaymentsRowsDataQuery(filterSortRequest), cancellationToken);
        return result.Match<PaymentsRowsDataResponse, Results<Ok<PaymentsRowsDataResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/payments/formlookups
    internal static async Task<Results<Ok<PaymentFormLookupsResponse>, ProblemHttpResult>> GetFormLookups(
        IQueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse> handler,
        CancellationToken cancellationToken = default)
    {
        Result<PaymentFormLookupsResponse> result =
            await handler.Handle(new GetPaymentFormLookupsQuery(), cancellationToken);
        return result.Match<PaymentFormLookupsResponse, Results<Ok<PaymentFormLookupsResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/payments/studentcontracts?academicYearId={id}
    internal static async Task<Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>> GetStudentContracts(
        [FromQuery] int academicYearId,
        IQueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>> handler,
        CancellationToken cancellationToken = default)
    {
        Result<List<LookupItemResponse>> result =
            await handler.Handle(new GetPaymentStudentContractsQuery(academicYearId), cancellationToken);
        return result.Match<List<LookupItemResponse>, Results<Ok<List<LookupItemResponse>>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // GET api/v1/payments/{paymentId:int}
    internal static async Task<Results<Ok<PaymentResponse>, ProblemHttpResult>> GetOne([FromRoute] int paymentId,
        IQueryHandler<GetPaymentQuery, PaymentResponse> handler, CancellationToken cancellationToken = default)
    {
        Result<PaymentResponse> result = await handler.Handle(new GetPaymentQuery(paymentId), cancellationToken);
        return result.Match<PaymentResponse, Results<Ok<PaymentResponse>, ProblemHttpResult>>(
            success => TypedResults.Ok(success), failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // POST api/v1/payments
    internal static async Task<Results<Ok<int>, ProblemHttpResult>> Create([FromBody] PaymentRequest request,
        ICommandHandler<CreatePaymentCommand, int> handler, CancellationToken cancellationToken = default)
    {
        Result<int> result = await handler.Handle(new CreatePaymentCommand(request), cancellationToken);
        return result.Match<int, Results<Ok<int>, ProblemHttpResult>>(success => TypedResults.Ok(success),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // PUT api/v1/payments/{paymentId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Update([FromRoute] int paymentId,
        [FromBody] PaymentRequest request, ICommandHandler<UpdatePaymentCommand> handler,
        CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new UpdatePaymentCommand(paymentId, request), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }

    // DELETE api/v1/payments/{paymentId:int}
    internal static async Task<Results<Ok, ProblemHttpResult>> Delete([FromRoute] int paymentId,
        ICommandHandler<DeletePaymentCommand> handler, CancellationToken cancellationToken = default)
    {
        Result result = await handler.Handle(new DeletePaymentCommand(paymentId), cancellationToken);
        return result.Match<Results<Ok, ProblemHttpResult>>(() => TypedResults.Ok(),
            failure => (ProblemHttpResult)CustomResults.Problem(failure));
    }
}
