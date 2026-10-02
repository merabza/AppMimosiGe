using AppMimosiGe.Application.WorkHours.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.WorkHours.StartWork;

// ReSharper disable once UnusedType.Global
public sealed class StartWorkCommandValidator : AbstractValidator<StartWorkCommand>
{
    public StartWorkCommandValidator(IWorkHoursRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new WorkTimeFixRequestValidator(repository)));
    }
}
