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

namespace AppMimosiGe.Application.Payments.CreatePayment;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreatePaymentCommandHandler(
    IPaymentsRepository repository,
    IUserClaimRights claimRights,
    IUnitOfWork unitOfWork) : ICommandHandler<CreatePaymentCommand, int>
{
    public async Task<Result<int>> Handle(CreatePaymentCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        PaymentRequest request = command.Request!;

        //შემოწმებულად მონიშვნა მხოლოდ გადახდების შემოწმების უფლებით შეიძლება (D77)
        if (request.Checked && !await claimRights.HasClaim(PaymentClaims.CheckPayments, cancellationToken))
        {
            return Result.Failure<int>(PaymentErrors.CheckedRequiresRight);
        }

        var payment = new Payment();
        PaymentMapper.ApplyFields(payment, request);
        repository.Add(payment);

        //Access-ის FrmPayments.Form_AfterUpdate (D78)
        await repository.MarkNextPayDatesDirty([payment.StudentContractId], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }
}
