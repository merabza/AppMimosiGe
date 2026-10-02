using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;

// ReSharper disable once UnusedType.Global
public sealed class AutoGenerateWorkHoursCommandValidator : AbstractValidator<AutoGenerateWorkHoursCommand>
{
    //Access-ში ცარიელი თარიღით (Null პარამეტრი) არაფერი იქმნებოდა
    public AutoGenerateWorkHoursCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).Must(r => r.DateFrom is not null && r.DateTo is not null)
                .WithErrorCode(WorkHourErrors.PeriodIsRequired.Code)
                .WithMessage(WorkHourErrors.PeriodIsRequired.Description);
            RuleFor(x => x.Request!).Must(r => r.DateFrom!.Value.Date <= r.DateTo!.Value.Date)
                .When(x => x.Request!.DateFrom is not null && x.Request.DateTo is not null)
                .WithErrorCode(WorkHourErrors.PeriodIsInvalid.Code)
                .WithMessage(WorkHourErrors.PeriodIsInvalid.Description);
        });
    }
}
