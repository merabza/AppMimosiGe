using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.GetPaymentFormLookups;

public sealed record GetPaymentFormLookupsQuery : IQuery<PaymentFormLookupsResponse>;
