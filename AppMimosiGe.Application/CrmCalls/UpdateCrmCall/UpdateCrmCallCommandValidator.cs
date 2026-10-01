using AppMimosiGe.Application.CrmCalls.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.CrmCalls.UpdateCrmCall;

// ReSharper disable once UnusedType.Global
public sealed class UpdateCrmCallCommandValidator : AbstractValidator<UpdateCrmCallCommand>
{
    public UpdateCrmCallCommandValidator(ICrmCallsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new CrmCallRequestValidator(repository)));
    }
}
