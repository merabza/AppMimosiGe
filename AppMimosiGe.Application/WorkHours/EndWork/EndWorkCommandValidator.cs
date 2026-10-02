using AppMimosiGe.Application.WorkHours.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.WorkHours.EndWork;

// ReSharper disable once UnusedType.Global
public sealed class EndWorkCommandValidator : AbstractValidator<EndWorkCommand>
{
    public EndWorkCommandValidator(IWorkHoursRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new WorkTimeFixRequestValidator(repository)));
    }
}
