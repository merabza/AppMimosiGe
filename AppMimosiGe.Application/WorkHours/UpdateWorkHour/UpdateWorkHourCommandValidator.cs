using AppMimosiGe.Application.WorkHours.Validation;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.WorkHours.UpdateWorkHour;

// ReSharper disable once UnusedType.Global
public sealed class UpdateWorkHourCommandValidator : AbstractValidator<UpdateWorkHourCommand>
{
    public UpdateWorkHourCommandValidator(IWorkHoursRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new WorkHourRequestValidator(repository)));
    }
}
