using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.UpdatePayment;

public sealed record UpdatePaymentCommand(int PaymentId, PaymentRequest? Request) : ICommand;
