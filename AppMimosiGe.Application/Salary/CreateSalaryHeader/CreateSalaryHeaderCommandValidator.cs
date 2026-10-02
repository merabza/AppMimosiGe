using AppMimosiGe.Application.Salary.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Salary.CreateSalaryHeader;

// ReSharper disable once UnusedType.Global
public sealed class CreateSalaryHeaderCommandValidator : AbstractValidator<CreateSalaryHeaderCommand>
{
    public CreateSalaryHeaderCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new SalaryHeaderRequestValidator()));
    }
}
