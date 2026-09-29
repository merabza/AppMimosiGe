using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.StudentContracts.Validation;

public sealed class StudentContractDetailRequestValidator : AbstractValidator<StudentContractDetailRequest>
{
    public StudentContractDetailRequestValidator(IStudentContractsRepository repository)
    {
        RuleFor(x => x.CourseId).MustAsync((id, ct) => repository.CourseExists(id, ct))
            .WithErrorCode(StudentContractErrors.CourseNotFound.Code)
            .WithMessage(StudentContractErrors.CourseNotFound.Description);
        RuleFor(x => x.GroupSizeId).MustAsync((id, ct) => repository.GroupSizeExists(id, ct))
            .WithErrorCode(StudentContractErrors.GroupSizeNotFound.Code)
            .WithMessage(StudentContractErrors.GroupSizeNotFound.Description);

        //Access ამას არ ამოწმებდა (D44): საათებზე იყოფა ერთი საათის ღირებულება, უარყოფითი თანხა კი აზრს მოკლებულია
        RuleFor(x => x.FourWeekHours).GreaterThan(0)
            .WithErrorCode(StudentContractErrors.FourWeekHoursMustBePositive.Code)
            .WithMessage(StudentContractErrors.FourWeekHoursMustBePositive.Description);
        RuleFor(x => x.FourWeekFee).GreaterThanOrEqualTo(0)
            .WithErrorCode(StudentContractErrors.FeeMustNotBeNegative.Code)
            .WithMessage(StudentContractErrors.FeeMustNotBeNegative.Description);
        RuleFor(x => x.OneHourFee).GreaterThanOrEqualTo(0)
            .WithErrorCode(StudentContractErrors.FeeMustNotBeNegative.Code)
            .WithMessage(StudentContractErrors.FeeMustNotBeNegative.Description);
    }
}
