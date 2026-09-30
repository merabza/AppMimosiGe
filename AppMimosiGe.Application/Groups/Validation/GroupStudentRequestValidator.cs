using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Groups.Validation;

public sealed class GroupStudentRequestValidator : AbstractValidator<GroupStudentRequest>
{
    //სვეტის სიგრძე (Access-ისაც)
    public const int NoteMaxLength = 255;

    public GroupStudentRequestValidator(IGroupsRepository repository)
    {
        RuleFor(x => x.StudentContractId).MustAsync((id, ct) => repository.StudentContractExists(id, ct))
            .WithErrorCode(GroupErrors.StudentContractNotFound.Code)
            .WithMessage(GroupErrors.StudentContractNotFound.Description);

        //Access-ის ველების წესი "valid=[>0]" (ბაზაში CHECK-ებიც ასეა)
        RuleFor(x => x.FourWeekHours).GreaterThan(0).WithErrorCode(GroupErrors.FourWeekHoursMustBePositive.Code)
            .WithMessage(GroupErrors.FourWeekHoursMustBePositive.Description);
        RuleFor(x => x.FourWeekFee).GreaterThan(0).WithErrorCode(GroupErrors.FeeMustBePositive.Code)
            .WithMessage(GroupErrors.FeeMustBePositive.Description);
        RuleFor(x => x.OneHourFee).GreaterThan(0).WithErrorCode(GroupErrors.FeeMustBePositive.Code)
            .WithMessage(GroupErrors.FeeMustBePositive.Description);
        RuleFor(x => x.HoursCoefficient).GreaterThan(0)
            .WithErrorCode(GroupErrors.HoursCoefficientMustBePositive.Code)
            .WithMessage(GroupErrors.HoursCoefficientMustBePositive.Description);

        RuleFor(x => x.StartDate).NotEmpty().WithErrorCode(GroupErrors.StartDateIsRequired.Code)
            .WithMessage(GroupErrors.StartDateIsRequired.Description);
        RuleFor(x => x.EndDate).Must((row, endDate) => endDate!.Value.Date > row.StartDate.Date)
            .When(x => x.EndDate is not null).WithErrorCode(GroupErrors.EndDateMustBeAfterStartDate.Code)
            .WithMessage(GroupErrors.EndDateMustBeAfterStartDate.Description);

        RuleFor(x => x.Note).MaximumLength(NoteMaxLength).WithErrorCode(GroupErrors.NoteIsTooLong.Code)
            .WithMessage(GroupErrors.NoteIsTooLong.Description);
    }
}
