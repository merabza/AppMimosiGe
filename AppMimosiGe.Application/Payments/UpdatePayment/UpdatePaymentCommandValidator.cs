using AppMimosiGe.Application.Payments.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Payments.UpdatePayment;

// ReSharper disable once UnusedType.Global
public sealed class UpdatePaymentCommandValidator : AbstractValidator<UpdatePaymentCommand>
{
    public UpdatePaymentCommandValidator(IPaymentsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new PaymentRequestValidator(repository)));
    }
}
