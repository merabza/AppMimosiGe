using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.CrmCalls.Validation;

/// <summary>
///     ზარის ველების შემოწმება, რომელიც შექმნასაც და შეცვლასაც სჭირდება. Access-ის crmCalls-ის REQ ველები:
///     კონტრაქტი, ტიპი, თარიღი და შედეგი. საუბრის შინაარსი Memo-ა (nvarchar(max)), ამიტომ სიგრძე არ იზღუდება
/// </summary>
public sealed class CrmCallRequestValidator : AbstractValidator<CrmCallRequest>
{
    public CrmCallRequestValidator(ICrmCallsRepository repository)
    {
        RuleFor(x => x.StudentContractId).MustAsync((id, ct) => repository.StudentContractExists(id, ct))
            .WithErrorCode(CrmCallErrors.StudentContractNotFound.Code)
            .WithMessage(CrmCallErrors.StudentContractNotFound.Description);

        RuleFor(x => x.CallTypeId).MustAsync((id, ct) => repository.CallTypeExists(id, ct))
            .WithErrorCode(CrmCallErrors.CallTypeNotFound.Code).WithMessage(CrmCallErrors.CallTypeNotFound.Description);

        RuleFor(x => x.CallDate).NotEmpty().WithErrorCode(CrmCallErrors.CallDateIsRequired.Code)
            .WithMessage(CrmCallErrors.CallDateIsRequired.Description);

        RuleFor(x => x.AnswerTypeId).NotNull().WithErrorCode(CrmCallErrors.AnswerTypeIsRequired.Code)
            .WithMessage(CrmCallErrors.AnswerTypeIsRequired.Description);
        RuleFor(x => x.AnswerTypeId).MustAsync((id, ct) => repository.AnswerTypeExists(id!.Value, ct))
            .When(x => x.AnswerTypeId is not null).WithErrorCode(CrmCallErrors.AnswerTypeNotFound.Code)
            .WithMessage(CrmCallErrors.AnswerTypeNotFound.Description);
    }
}
