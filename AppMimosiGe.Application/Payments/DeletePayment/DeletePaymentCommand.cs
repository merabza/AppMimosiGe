using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.DeletePayment;

public sealed record DeletePaymentCommand(int PaymentId) : ICommand;
