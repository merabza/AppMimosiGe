using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.CreatePayment;

//პასუხი ახალი გადახდის იდენტიფიკატორია
public sealed record CreatePaymentCommand(PaymentRequest? Request) : ICommand<int>;
