using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.Groups.UpdateGroup;

// ReSharper disable once UnusedType.Global
public sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator(IGroupsRepository repository,
        IStudentContractsRepository studentContractsRepository, ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).SetValidator(new GroupRequestValidator(repository, studentContractsRepository,
                teacherContractsRepository));

            //შესაცვლელი ჯგუფი თავის თავს დუბლიკატად არ ითვლის
            RuleFor(x => x)
                .MustAsync(async (command, ct) => !await repository.GroupCodeExists(command.Request!.AcademicYearId,
                    command.Request.GroupCode!.Trim(), command.GrpId, ct))
                .When(x => !string.IsNullOrWhiteSpace(x.Request!.GroupCode))
                .WithErrorCode(GroupErrors.GroupCodeAlreadyExists.Code)
                .WithMessage(GroupErrors.GroupCodeAlreadyExists.Description);
        });
    }
}
