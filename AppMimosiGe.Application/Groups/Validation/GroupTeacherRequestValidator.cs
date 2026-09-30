using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Groups.Validation;

public sealed class GroupTeacherRequestValidator : AbstractValidator<GroupTeacherRequest>
{
    public GroupTeacherRequestValidator(IGroupsRepository repository,
        ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.TeacherContractId).MustAsync((id, ct) => repository.TeacherContractExists(id, ct))
            .WithErrorCode(GroupErrors.TeacherContractNotFound.Code)
            .WithMessage(GroupErrors.TeacherContractNotFound.Description);

        RuleFor(x => x.SalarySchemaId)
            .MustAsync((id, ct) => teacherContractsRepository.SalarySchemeExists(id!.Value, ct))
            .When(x => x.SalarySchemaId is not null).WithErrorCode(GroupErrors.SalarySchemeNotFound.Code)
            .WithMessage(GroupErrors.SalarySchemeNotFound.Description);

        //სქემის გარეშე მოსულ სტრიქონს კონტრაქტის ძირითადი სქემა ეწერება, ამიტომ კონტრაქტს ის უნდა ჰქონდეს
        RuleFor(x => x.TeacherContractId)
            .MustAsync(async (id, ct) => await repository.GetDefaultSalarySchemeId(id, ct) is not null)
            .When(x => x.SalarySchemaId is null).WithErrorCode(GroupErrors.SalarySchemeIsRequired.Code)
            .WithMessage(GroupErrors.SalarySchemeIsRequired.Description);

        RuleFor(x => x.StartDate).NotEmpty().WithErrorCode(GroupErrors.StartDateIsRequired.Code)
            .WithMessage(GroupErrors.StartDateIsRequired.Description);
        RuleFor(x => x.EndDate).Must((row, endDate) => endDate!.Value.Date > row.StartDate.Date)
            .When(x => x.EndDate is not null).WithErrorCode(GroupErrors.EndDateMustBeAfterStartDate.Code)
            .WithMessage(GroupErrors.EndDateMustBeAfterStartDate.Description);
    }
}
