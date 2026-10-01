using AppMimosiGe.Application.Payments.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Payments.CreatePayment;

// ReSharper disable once UnusedType.Global
public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator(IPaymentsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new PaymentRequestValidator(repository)));
    }
}
