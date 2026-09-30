using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Groups.CreateGroup;

// ReSharper disable once UnusedType.Global
public sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator(IGroupsRepository repository,
        IStudentContractsRepository studentContractsRepository,
        ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).SetValidator(new GroupRequestValidator(repository, studentContractsRepository,
                teacherContractsRepository));

            RuleFor(x => x.Request!)
                .MustAsync(async (request, ct) =>
                    !await repository.GroupCodeExists(request.AcademicYearId, request.GroupCode!.Trim(), 0, ct))
                .When(x => !string.IsNullOrWhiteSpace(x.Request!.GroupCode))
                .WithErrorCode(GroupErrors.GroupCodeAlreadyExists.Code)
                .WithMessage(GroupErrors.GroupCodeAlreadyExists.Description);
        });
    }
}
