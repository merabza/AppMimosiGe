using AppMimosiGe.Application.Salary.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Salary.CreateSalaryPart;

// ReSharper disable once UnusedType.Global
public sealed class CreateSalaryPartCommandValidator : AbstractValidator<CreateSalaryPartCommand>
{
    public CreateSalaryPartCommandValidator(ISalaryRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new SalaryPartRequestValidator(repository)));
    }
}
