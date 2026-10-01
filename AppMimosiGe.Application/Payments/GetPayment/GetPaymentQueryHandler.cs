using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.GetPayment;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetPaymentQueryHandler(IPaymentsRepository repository)
    : IQueryHandler<GetPaymentQuery, PaymentResponse>
{
    public async Task<Result<PaymentResponse>> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
    {
        PaymentResponse? payment = await repository.GetOne(request.PaymentId, cancellationToken);
        return payment is null ? PaymentErrors.PaymentNotFound : payment;
    }
}
