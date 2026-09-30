using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Groups.Validation;

public sealed class GroupDayTimePlaceRequestValidator : AbstractValidator<GroupDayTimePlaceRequest>
{
    public GroupDayTimePlaceRequestValidator(IGroupsRepository repository)
    {
        RuleFor(x => x.WeekDayId).MustAsync((id, ct) => repository.WeekDayExists(id, ct))
            .WithErrorCode(GroupErrors.WeekDayNotFound.Code).WithMessage(GroupErrors.WeekDayNotFound.Description);
        RuleFor(x => x.LessonStartTimeId).MustAsync((id, ct) => repository.LessonStartTimeExists(id, ct))
            .WithErrorCode(GroupErrors.LessonStartTimeNotFound.Code)
            .WithMessage(GroupErrors.LessonStartTimeNotFound.Description);
        RuleFor(x => x.RoomId).MustAsync((id, ct) => repository.RoomExists(id, ct))
            .WithErrorCode(GroupErrors.RoomNotFound.Code).WithMessage(GroupErrors.RoomNotFound.Description);

        //Access ამას არ ამოწმებდა: გაკვეთილი 0 ან უარყოფითი საათით აზრს მოკლებულია
        RuleFor(x => x.HoursCount).GreaterThan(0).WithErrorCode(GroupErrors.HoursCountMustBePositive.Code)
            .WithMessage(GroupErrors.HoursCountMustBePositive.Description);

        RuleFor(x => x.StartDate).NotEmpty().WithErrorCode(GroupErrors.StartDateIsRequired.Code)
            .WithMessage(GroupErrors.StartDateIsRequired.Description);
        RuleFor(x => x.EndDate).Must((row, endDate) => endDate!.Value.Date > row.StartDate.Date)
            .When(x => x.EndDate is not null).WithErrorCode(GroupErrors.EndDateMustBeAfterStartDate.Code)
            .WithMessage(GroupErrors.EndDateMustBeAfterStartDate.Description);
    }
}
