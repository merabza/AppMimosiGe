using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.SetPaymentChecked;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SetPaymentCheckedCommandHandler(IPaymentsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<SetPaymentCheckedCommand>
{
    //მხოლოდ ალამი იცვლება. გადახდების შემოწმების უფლებას endpoint-ის ფილტრი ამოწმებს (403). ალამი ბალანსს და შემდეგი
    //გადახდის თარიღს არ ცვლის, ამიტომ კონტრაქტს DirtyNextPayDate არ ერთვება (D152)
    public async Task<Result> Handle(SetPaymentCheckedCommand command, CancellationToken cancellationToken)
    {
        Payment? payment = await repository.GetForChange(command.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(PaymentErrors.PaymentNotFound);
        }

        payment.Checked = command.Checked;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
