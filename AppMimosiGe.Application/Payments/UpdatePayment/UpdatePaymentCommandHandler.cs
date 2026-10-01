using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments.Validation;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.UpdatePayment;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdatePaymentCommandHandler(
    IPaymentsRepository repository,
    IUserClaimRights claimRights,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdatePaymentCommand>
{
    public async Task<Result> Handle(UpdatePaymentCommand command, CancellationToken cancellationToken)
    {
        PaymentRequest request = command.Request!;

        Payment? payment = await repository.GetForChange(command.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(PaymentErrors.PaymentNotFound);
        }

        //შემოწმებული გადახდის შეცვლა და ალმის დასმა მხოლოდ გადახდების შემოწმების უფლებით შეიძლება (D77)
        if ((payment.Checked || request.Checked) &&
            !await claimRights.HasClaim(PaymentClaims.CheckPayments, cancellationToken))
        {
            return Result.Failure(payment.Checked
                ? PaymentErrors.PaymentIsChecked
                : PaymentErrors.CheckedRequiresRight);
        }

        //კონტრაქტის შეცვლისას ძველი კონტრაქტის ბალანსიც იცვლება
        int oldStudentContractId = payment.StudentContractId;
        PaymentMapper.ApplyFields(payment, request);

        //Access-ის FrmPayments.Form_AfterUpdate (D78)
        await repository.MarkNextPayDatesDirty([oldStudentContractId, payment.StudentContractId], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
