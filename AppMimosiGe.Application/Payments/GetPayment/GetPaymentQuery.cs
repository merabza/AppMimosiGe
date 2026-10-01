using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.GetPayment;

public sealed record GetPaymentQuery(int PaymentId) : IQuery<PaymentResponse>;
