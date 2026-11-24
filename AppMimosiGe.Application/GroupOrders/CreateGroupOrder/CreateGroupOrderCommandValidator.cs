using AppMimosiGe.Application.GroupOrders.Validation;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.GroupOrders.CreateGroupOrder;

// ReSharper disable once UnusedType.Global
public sealed class CreateGroupOrderCommandValidator : AbstractValidator<CreateGroupOrderCommand>
{
    public CreateGroupOrderCommandValidator(IGroupOrdersRepository repository, IGroupsRepository groupsRepository,
        IStudentContractsRepository studentContractsRepository, ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null,
            () => RuleFor(x => x.Request!).SetValidator(new GroupOrderRequestValidator(repository, groupsRepository,
                studentContractsRepository, teacherContractsRepository)));
    }
}
