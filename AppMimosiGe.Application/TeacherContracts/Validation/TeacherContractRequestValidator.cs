using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.TeacherContracts.Validation;

/// <summary>
///     კონტრაქტის ველების შემოწმება, რომელიც შექმნასაც და შეცვლასაც სჭირდება. ნომრის უნიკალურობას ბრძანების
///     ვალიდატორი ამოწმებს, რადგან შეცვლისას საკუთარი ჩანაწერი გამოსარიცხია
/// </summary>
public sealed class TeacherContractRequestValidator : AbstractValidator<TeacherContractRequest>
{
    //Access-ის ფორმის შეყვანის ნიღაბი "\T0.00": ასო T, ციფრი, წერტილი და ორი ციფრი (მაგალითად T3.01)
    public const string ContractNumberPattern = @"^T\d\.\d{2}$";

    //სვეტების სიგრძეები (Access-ისაც)
    public const int BankAccountMaxLength = 22;
    public const int BankAccountCodeMaxLength = 8;
    public const int DescriptionMaxLength = 255;

    public TeacherContractRequestValidator(ITeacherContractsRepository repository)
    {
        RuleFor(x => x.ContractNumber).NotEmpty().WithErrorCode(TeacherContractErrors.ContractNumberIsRequired.Code)
            .WithMessage(TeacherContractErrors.ContractNumberIsRequired.Description);
        RuleFor(x => x.ContractNumber).Matches(ContractNumberPattern)
            .When(x => !string.IsNullOrEmpty(x.ContractNumber))
            .WithErrorCode(TeacherContractErrors.ContractNumberFormatIsInvalid.Code)
            .WithMessage(TeacherContractErrors.ContractNumberFormatIsInvalid.Description);

        RuleFor(x => x.ContractDate).NotEmpty().WithErrorCode(TeacherContractErrors.ContractDateIsRequired.Code)
            .WithMessage(TeacherContractErrors.ContractDateIsRequired.Description);

        RuleFor(x => x.TeacherHumanId).MustAsync((id, ct) => repository.HumanExists(id, ct))
            .WithErrorCode(TeacherContractErrors.TeacherNotFound.Code)
            .WithMessage(TeacherContractErrors.TeacherNotFound.Description);

        RuleFor(x => x.BankAccount).MaximumLength(BankAccountMaxLength)
            .WithErrorCode(TeacherContractErrors.BankAccountIsTooLong.Code)
            .WithMessage(TeacherContractErrors.BankAccountIsTooLong.Description);
        RuleFor(x => x.BankAccountCode).MaximumLength(BankAccountCodeMaxLength)
            .WithErrorCode(TeacherContractErrors.BankAccountCodeIsTooLong.Code)
            .WithMessage(TeacherContractErrors.BankAccountCodeIsTooLong.Description);

        RuleFor(x => x.RsQuoteTypeId).MustAsync((id, ct) => repository.RsQuoteTypeExists(id!.Value, ct))
            .When(x => x.RsQuoteTypeId is not null).WithErrorCode(TeacherContractErrors.RsQuoteTypeNotFound.Code)
            .WithMessage(TeacherContractErrors.RsQuoteTypeNotFound.Description);
        RuleFor(x => x.RsCountryId).MustAsync((id, ct) => repository.RsCountryExists(id, ct))
            .WithErrorCode(TeacherContractErrors.RsCountryNotFound.Code)
            .WithMessage(TeacherContractErrors.RsCountryNotFound.Description);

        RuleFor(x => x.FixedAmount).GreaterThanOrEqualTo(0)
            .WithErrorCode(TeacherContractErrors.FixedAmountMustNotBeNegative.Code)
            .WithMessage(TeacherContractErrors.FixedAmountMustNotBeNegative.Description);

        RuleFor(x => x.Description).MaximumLength(DescriptionMaxLength)
            .WithErrorCode(TeacherContractErrors.DescriptionIsTooLong.Code)
            .WithMessage(TeacherContractErrors.DescriptionIsTooLong.Description);

        RuleFor(x => x.SalarySchemaByHoursId).MustAsync((id, ct) => repository.SalarySchemeExists(id!.Value, ct))
            .When(x => x.SalarySchemaByHoursId is not null)
            .WithErrorCode(TeacherContractErrors.SalarySchemeNotFound.Code)
            .WithMessage(TeacherContractErrors.SalarySchemeNotFound.Description);
        RuleFor(x => x.WorkHourGroupId).MustAsync((id, ct) => repository.WorkHourGroupExists(id!.Value, ct))
            .When(x => x.WorkHourGroupId is not null)
            .WithErrorCode(TeacherContractErrors.WorkHourGroupNotFound.Code)
            .WithMessage(TeacherContractErrors.WorkHourGroupNotFound.Description);

        //ორივე დრო ერთი დღისაა (1899-12-30), ამიტომ დაწყება დასრულებაზე ადრე უნდა იყოს
        RuleFor(x => x.WorkHoursStart).LessThan(x => x.WorkHoursEnd)
            .When(x => x.WorkHoursStart is not null && x.WorkHoursEnd is not null)
            .WithErrorCode(TeacherContractErrors.WorkHoursStartMustBeBeforeEnd.Code)
            .WithMessage(TeacherContractErrors.WorkHoursStartMustBeBeforeEnd.Description);

        RuleFor(x => x.ContractEndDate).GreaterThanOrEqualTo(x => x.ContractDate.Date)
            .When(x => x.ContractEndDate is not null)
            .WithErrorCode(TeacherContractErrors.ContractEndDateIsBeforeContractDate.Code)
            .WithMessage(TeacherContractErrors.ContractEndDateIsBeforeContractDate.Description);
    }
}
