using AppMimosiGe.Application.Salary.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Salary.UpdateSalaryPart;

// ReSharper disable once UnusedType.Global
public sealed class UpdateSalaryPartCommandValidator : AbstractValidator<UpdateSalaryPartCommand>
{
    public UpdateSalaryPartCommandValidator(ISalaryRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new SalaryPartRequestValidator(repository)));
    }
}
