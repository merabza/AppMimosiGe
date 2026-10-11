using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Payments.SetPaymentChecked;

//"შემოწმებულია" ალამი პირდაპირ სიიდან (D152)
public sealed record SetPaymentCheckedCommand(int PaymentId, bool Checked) : ICommand;
