using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Groups.Validation;

/// <summary>
///     ჯგუფის ველების და სტრიქონების შემოწმება, რომელიც შექმნასაც და შეცვლასაც სჭირდება. კოდის უნიკალურობას ბრძანების
///     ვალიდატორი ამოწმებს, რადგან შეცვლისას საკუთარი ჩანაწერი გამოსარიცხია
/// </summary>
public sealed class GroupRequestValidator : AbstractValidator<GroupRequest>
{
    //სვეტის სიგრძე (Access-ისაც)
    public const int GroupCodeMaxLength = 5;

    public GroupRequestValidator(IGroupsRepository repository, IStudentContractsRepository studentContractsRepository,
        ITeacherContractsRepository teacherContractsRepository)
    {
        RuleFor(x => x.AcademicYearId).MustAsync((id, ct) => studentContractsRepository.AcademicYearExists(id, ct))
            .WithErrorCode(GroupErrors.AcademicYearNotFound.Code)
            .WithMessage(GroupErrors.AcademicYearNotFound.Description);

        RuleFor(x => x.GroupCode).NotEmpty().WithErrorCode(GroupErrors.GroupCodeIsRequired.Code)
            .WithMessage(GroupErrors.GroupCodeIsRequired.Description);
        RuleFor(x => x.GroupCode).Must(code => code!.Trim().Length <= GroupCodeMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.GroupCode)).WithErrorCode(GroupErrors.GroupCodeIsTooLong.Code)
            .WithMessage(GroupErrors.GroupCodeIsTooLong.Description);

        RuleFor(x => x.CourseId).MustAsync((id, ct) => studentContractsRepository.CourseExists(id, ct))
            .WithErrorCode(GroupErrors.CourseNotFound.Code).WithMessage(GroupErrors.CourseNotFound.Description);
        RuleFor(x => x.GroupSizeId).MustAsync((id, ct) => studentContractsRepository.GroupSizeExists(id, ct))
            .WithErrorCode(GroupErrors.GroupSizeNotFound.Code)
            .WithMessage(GroupErrors.GroupSizeNotFound.Description);
        RuleFor(x => x.StudentStatusId)
            .MustAsync((id, ct) => studentContractsRepository.StudentStatusExists(id, ct))
            .WithErrorCode(GroupErrors.StudentStatusNotFound.Code)
            .WithMessage(GroupErrors.StudentStatusNotFound.Description);

        RuleFor(x => x.Teachers).NotNull();
        RuleForEach(x => x.Teachers)
            .SetValidator(new GroupTeacherRequestValidator(repository, teacherContractsRepository));
        RuleFor(x => x.Students).NotNull();
        RuleForEach(x => x.Students).SetValidator(new GroupStudentRequestValidator(repository));
        RuleFor(x => x.DayTimePlaces).NotNull();
        RuleForEach(x => x.DayTimePlaces).SetValidator(new GroupDayTimePlaceRequestValidator(repository));

        //Access გადაფარვას არ ამოწმებდა და გენერატორი ასეთ დღეებზე შეცდომას წერდა (5 და 7). მომხმარებლის
        //გადაწყვეტილებით შენახვა იბლოკება (D55)
        RuleFor(x => x.Teachers).Must(teachers => !GroupPeriods.AnyTeacherPeriodsOverlap(teachers))
            .When(x => x.Teachers is not null).WithErrorCode(GroupErrors.TeacherPeriodsOverlap.Code)
            .WithMessage(GroupErrors.TeacherPeriodsOverlap.Description);
        RuleFor(x => x.DayTimePlaces)
            .Must(dayTimePlaces => !GroupPeriods.AnyDayTimePlacePeriodsOverlap(dayTimePlaces))
            .When(x => x.DayTimePlaces is not null).WithErrorCode(GroupErrors.DayTimePlacePeriodsOverlap.Code)
            .WithMessage(GroupErrors.DayTimePlacePeriodsOverlap.Description);
    }
}
