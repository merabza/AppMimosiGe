using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.StudentContracts.Validation;

/// <summary>
///     კონტრაქტის ველების შემოწმება, რომელიც შექმნასაც და შეცვლასაც სჭირდება. ნომრის უნიკალურობას ბრძანების
///     ვალიდატორი ამოწმებს, რადგან შეცვლისას საკუთარი ჩანაწერი გამოსარიცხია
/// </summary>
public sealed class StudentContractRequestValidator : AbstractValidator<StudentContractRequest>
{
    //Access-ის ფორმის შეყვანის ნიღაბი "0.000": ერთი ციფრი, წერტილი და სამი ციფრი (სულ 5 სიმბოლო, სვეტის სიგრძე)
    public const string ContractNumberPattern = @"^\d\.\d{3}$";

    //Access-ის ველის წესი "Is Null Or <29": დღე ყოველ თვეში უნდა არსებობდეს
    public const int MaxDesiredMonthlyPaymentDay = 28;

    public StudentContractRequestValidator(IStudentContractsRepository repository)
    {
        RuleFor(x => x.ContractNumber).NotEmpty().WithErrorCode(StudentContractErrors.ContractNumberIsRequired.Code)
            .WithMessage(StudentContractErrors.ContractNumberIsRequired.Description);
        RuleFor(x => x.ContractNumber).Matches(ContractNumberPattern).When(x => !string.IsNullOrEmpty(x.ContractNumber))
            .WithErrorCode(StudentContractErrors.ContractNumberFormatIsInvalid.Code)
            .WithMessage(StudentContractErrors.ContractNumberFormatIsInvalid.Description);

        RuleFor(x => x.ContractDate).NotEmpty().WithErrorCode(StudentContractErrors.ContractDateIsRequired.Code)
            .WithMessage(StudentContractErrors.ContractDateIsRequired.Description);

        //გადამხდელი შეიძლება თვითონ მოსწავლეც იყოს, ამიტომ მოსწავლისა და გადამხდელის განსხვავება არ მოწმდება
        RuleFor(x => x.StudentHumanId).MustAsync((id, ct) => repository.HumanExists(id, ct))
            .WithErrorCode(StudentContractErrors.StudentNotFound.Code)
            .WithMessage(StudentContractErrors.StudentNotFound.Description);
        RuleFor(x => x.PayerHumanId).MustAsync((id, ct) => repository.HumanExists(id, ct))
            .WithErrorCode(StudentContractErrors.PayerNotFound.Code)
            .WithMessage(StudentContractErrors.PayerNotFound.Description);
        RuleFor(x => x.AcademicYearId).MustAsync((id, ct) => repository.AcademicYearExists(id, ct))
            .WithErrorCode(StudentContractErrors.AcademicYearNotFound.Code)
            .WithMessage(StudentContractErrors.AcademicYearNotFound.Description);
        RuleFor(x => x.StudentStatusId).MustAsync((id, ct) => repository.StudentStatusExists(id!.Value, ct))
            .When(x => x.StudentStatusId is not null).WithErrorCode(StudentContractErrors.StudentStatusNotFound.Code)
            .WithMessage(StudentContractErrors.StudentStatusNotFound.Description);

        RuleFor(x => x.DesiredMonthlyPaymentDay).InclusiveBetween(1, MaxDesiredMonthlyPaymentDay)
            .When(x => x.DesiredMonthlyPaymentDay is not null)
            .WithErrorCode(StudentContractErrors.DesiredMonthlyPaymentDayIsOutOfRange.Code)
            .WithMessage(StudentContractErrors.DesiredMonthlyPaymentDayIsOutOfRange.Description);

        RuleFor(x => x.Details).NotNull();
        RuleForEach(x => x.Details).SetValidator(new StudentContractDetailRequestValidator(repository));
    }
}
