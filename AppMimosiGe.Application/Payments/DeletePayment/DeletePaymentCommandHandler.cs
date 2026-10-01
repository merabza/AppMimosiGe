using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.DeletePayment;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeletePaymentCommandHandler(
    IPaymentsRepository repository,
    IUserClaimRights claimRights,
    IUnitOfWork unitOfWork) : ICommandHandler<DeletePaymentCommand>
{
    public async Task<Result> Handle(DeletePaymentCommand command, CancellationToken cancellationToken)
    {
        Payment? payment = await repository.GetForChange(command.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(PaymentErrors.PaymentNotFound);
        }

        //შემოწმებული გადახდის წაშლა მხოლოდ გადახდების შემოწმების უფლებით შეიძლება (D77)
        if (payment.Checked && !await claimRights.HasClaim(PaymentClaims.CheckPayments, cancellationToken))
        {
            return Result.Failure(PaymentErrors.PaymentIsChecked);
        }

        repository.Remove(payment);

        //Access-ის FrmPayments.Form_AfterDelConfirm (D78)
        await repository.MarkNextPayDatesDirty([payment.StudentContractId], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
