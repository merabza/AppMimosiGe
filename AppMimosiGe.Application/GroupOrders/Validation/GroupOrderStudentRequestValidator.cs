using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.GroupOrders.Validation;

//ჯგუფის მოსწავლის სტრიქონის წესები (GroupStudentRequestValidator). რომელი ველია საჭირო, ბრძანების ტიპზეა
//დამოკიდებული და ამას ბრძანების ვალიდატორი ამოწმებს; აქ მოწმდება შევსებული ველები
public sealed class GroupOrderStudentRequestValidator : AbstractValidator<GroupOrderStudentRequest>
{
    public GroupOrderStudentRequestValidator(IGroupsRepository repository)
    {
        RuleFor(x => x.StudentContractId).MustAsync((id, ct) => repository.StudentContractExists(id, ct))
            .WithErrorCode(GroupErrors.StudentContractNotFound.Code)
            .WithMessage(GroupErrors.StudentContractNotFound.Description);

        RuleFor(x => x.FourWeekHours).GreaterThan(0).When(x => x.FourWeekHours is not null)
            .WithErrorCode(GroupErrors.FourWeekHoursMustBePositive.Code)
            .WithMessage(GroupErrors.FourWeekHoursMustBePositive.Description);
        RuleFor(x => x.FourWeekFee).GreaterThan(0).When(x => x.FourWeekFee is not null)
            .WithErrorCode(GroupErrors.FeeMustBePositive.Code).WithMessage(GroupErrors.FeeMustBePositive.Description);
        RuleFor(x => x.OneHourFee).GreaterThan(0).When(x => x.OneHourFee is not null)
            .WithErrorCode(GroupErrors.FeeMustBePositive.Code).WithMessage(GroupErrors.FeeMustBePositive.Description);
        RuleFor(x => x.HoursCoefficient).GreaterThan(0).When(x => x.HoursCoefficient is not null)
            .WithErrorCode(GroupErrors.HoursCoefficientMustBePositive.Code)
            .WithMessage(GroupErrors.HoursCoefficientMustBePositive.Description);

        RuleFor(x => x.Note).MaximumLength(GroupStudentRequestValidator.NoteMaxLength)
            .WithErrorCode(GroupErrors.NoteIsTooLong.Code).WithMessage(GroupErrors.NoteIsTooLong.Description);
    }
}
