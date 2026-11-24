using AppMimosiGe.Application.Groups;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.GroupOrders.Validation;

//ჯგუფის განრიგის სტრიქონის წესები (GroupDayTimePlaceRequestValidator, D181-ის 00:00-ის ჩათვლით); თარიღებს ტიპის
//მიხედვით ბრძანების ვალიდატორი ამოწმებს
public sealed class GroupOrderDayTimePlaceRequestValidator : AbstractValidator<GroupOrderDayTimePlaceRequest>
{
    public GroupOrderDayTimePlaceRequestValidator(IGroupsRepository repository)
    {
        RuleFor(x => x.WeekDayId).MustAsync((id, ct) => repository.WeekDayExists(id, ct))
            .WithErrorCode(GroupErrors.WeekDayNotFound.Code).WithMessage(GroupErrors.WeekDayNotFound.Description);
        RuleFor(x => x.LessonStartTimeId).MustAsync((id, ct) => repository.LessonStartTimeExists(id, ct))
            .WithErrorCode(GroupErrors.LessonStartTimeNotFound.Code)
            .WithMessage(GroupErrors.LessonStartTimeNotFound.Description);
        RuleFor(x => x.LessonStartTimeId)
            .MustAsync(async (id, ct) =>
                await repository.GetLessonStartTime(id, ct) is not { Hour: 0, Minute: 0, Second: 0 })
            .WithErrorCode(GroupErrors.LessonStartTimeIsMidnight.Code)
            .WithMessage(GroupErrors.LessonStartTimeIsMidnight.Description);
        RuleFor(x => x.RoomId).MustAsync((id, ct) => repository.RoomExists(id, ct))
            .WithErrorCode(GroupErrors.RoomNotFound.Code).WithMessage(GroupErrors.RoomNotFound.Description);
        RuleFor(x => x.HoursCount).GreaterThan(0).WithErrorCode(GroupErrors.HoursCountMustBePositive.Code)
            .WithMessage(GroupErrors.HoursCountMustBePositive.Description);
    }
}
