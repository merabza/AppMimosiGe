using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.GroupOrders.Validation;

//ჯგუფის მასწავლებლის სტრიქონის წესები (GroupTeacherRequestValidator); თარიღებს ტიპის მიხედვით ბრძანების ვალიდატორი
//ამოწმებს, პერიოდებს კი სამიზნე მდგომარეობის შემოწმება
public sealed class GroupOrderTeacherRequestValidator : AbstractValidator<GroupOrderTeacherRequest>
{
    public GroupOrderTeacherRequestValidator(IGroupsRepository repository,
        ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.TeacherContractId).MustAsync((id, ct) => repository.TeacherContractExists(id, ct))
            .WithErrorCode(GroupErrors.TeacherContractNotFound.Code)
            .WithMessage(GroupErrors.TeacherContractNotFound.Description);

        RuleFor(x => x.SalarySchemaId)
            .MustAsync((id, ct) => teacherContractsRepository.SalarySchemeExists(id!.Value, ct))
            .When(x => x.SalarySchemaId is not null).WithErrorCode(GroupErrors.SalarySchemeNotFound.Code)
            .WithMessage(GroupErrors.SalarySchemeNotFound.Description);

        //სქემის გარეშე მოსულ ხაზს კონტრაქტის ძირითადი სქემა ეწერება (D187), ამიტომ კონტრაქტს ის უნდა ჰქონდეს
        RuleFor(x => x.TeacherContractId)
            .MustAsync(async (id, ct) => await repository.GetDefaultSalarySchemeId(id, ct) is not null)
            .When(x => x.SalarySchemaId is null).WithErrorCode(GroupErrors.SalarySchemeIsRequired.Code)
            .WithMessage(GroupErrors.SalarySchemeIsRequired.Description);
    }
}
