using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;

/// <summary>
///     ვიზარდი 1 (D194, D196): ამოწმებს ჯგუფებს, რომლებიც საწყის ბრძანებას მიიღებენ, და თუ დარღვევა არ არის, ერთ
///     ტრანზაქციაში ქმნის საწყის ბრძანებებს, ჯგუფის ყოველ სტრიქონს თავის ხაზს უკავშირებს და რეჟიმს ცვლის. ნამდვილი
///     გაშვებაც ყველაფერს თავიდან ამოწმებს, იმავე ჩატვირთულ ჯგუფებზე, რომლებსაც ჩაწერს; თუ დარღვევაა, რეპორტს
///     აბრუნებს და არაფერს ცვლის (D219). გადასვლის დღე (D0) დღევანდელი დღეა
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SwitchToGroupOrdersCommandHandler(
    IGroupOrdersRepository repository,
    IGroupsRepository groupsRepository,
    IValidator<GroupRequest> groupValidator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<SwitchToGroupOrdersCommand, SwitchToGroupOrdersResponse>
{
    public async Task<Result<SwitchToGroupOrdersResponse>> Handle(SwitchToGroupOrdersCommand command,
        CancellationToken cancellationToken)
    {
        GroupOrdersMode? mode = await repository.GetModeForChange(cancellationToken);
        if (mode is { IsOrdersMode: true })
        {
            return GroupOrderErrors.GroupOrdersAreAlreadyEnabled;
        }

        DateTime now = timeProvider.GetLocalNow().DateTime;
        DateTime transitionDate = now.Date;
        List<Group> groups = await repository.GetAllGroupsForChange(cancellationToken);
        Dictionary<int, GroupState> states = groups.ToDictionary(g => g.GrpId, GroupStateMapper.ToState);
        GroupOrdersSwitchPlan plan = await Check(states, transitionDate, cancellationToken);
        if (command.DryRun || plan.Violations.Count > 0)
        {
            return ToResponse(plan, command.DryRun, false, transitionDate);
        }

        Dictionary<int, Group> groupsById = groups.ToDictionary(g => g.GrpId);
        foreach (GroupOrdersSwitchGroup planned in plan.Groups)
        {
            if (planned.InitialOrder is { } initialOrder)
            {
                repository.AddOrder(GroupOrdersSwitchWriter.CreateInitialOrder(groupsById[planned.GroupId],
                    initialOrder, currentUser.Name, now));
            }
        }

        if (mode is null)
        {
            mode = new GroupOrdersMode { Id = GroupOrdersMode.SingleRowId };
            repository.AddMode(mode);
        }

        mode.IsOrdersMode = true;
        mode.TransitionDate = transitionDate;
        mode.SwitchedByUserName = currentUser.Name;
        mode.SwitchedAt = now;

        //ერთი SaveChanges, ერთი ტრანზაქცია. ერთდროული გაშვებისას (რეჟიმის SwitchedAt, D221), ან თუ ამასობაში ჯგუფის
        //სტრიქონი წაიშალა, მთლიანად უარყოფილია
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return GroupOrderErrors.ModeSwitchedConcurrently;
        }

        return ToResponse(plan, false, true, transitionDate);
    }

    //დამგეგმავის ფორმულები D0-დან, ჯგუფის რედაქტორის ვალიდატორები მთელ ისტორიაზე და დროების გადაფარვა D0-დან
    //(D194, D212), მხოლოდ საწყისი ბრძანების მიმღებ ჯგუფებზე. ძველ შეცდომიან გაკვეთილებს (D177) ვიზარდი არ ამოწმებს
    private async Task<GroupOrdersSwitchPlan> Check(IReadOnlyDictionary<int, GroupState> states,
        DateTime transitionDate, CancellationToken cancellationToken)
    {
        GroupOrdersSwitchNames names = await Names(states.Values, cancellationToken);
        GroupOrdersSwitchPlan plan =
            GroupOrdersSwitchPlanner.Plan(states, transitionDate, id => names.Students[id]);
        int[] plannedGroupIds = [.. plan.Groups.Where(g => g.InitialOrder is not null).Select(g => g.GroupId)];

        List<GroupOrdersSwitchViolation> violations = [];
        foreach (int groupId in plannedGroupIds)
        {
            ValidationResult result = await groupValidator.ValidateAsync(
                GroupStateMapper.ToGroupRequest(states[groupId]), cancellationToken);
            violations.AddRange(GroupOrdersSwitchChecks.ValidationViolations(groupId, states[groupId],
                result.Errors.Select(f =>
                    new GroupValidationFailure(f.PropertyName, Error.Problem(f.ErrorCode, f.ErrorMessage))), names));
        }

        violations.AddRange(GroupOrdersSwitchChecks.TimesOverlaps(states, plannedGroupIds, transitionDate, names));
        return GroupOrdersSwitchChecks.AddViolations(plan, violations);
    }

    private async Task<GroupOrdersSwitchNames> Names(IEnumerable<GroupState> states,
        CancellationToken cancellationToken)
    {
        Dictionary<int, string> students = await groupsRepository.GetStudentContractNames(
            [.. states.SelectMany(s => s.Students).Select(s => s.StudentContractId).Distinct()], cancellationToken);
        Dictionary<int, string> teachers =
            (await groupsRepository.GetTeacherContracts(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        Dictionary<int, string> weekDays =
            (await groupsRepository.GetWeekDays(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        Dictionary<int, TimeOnly> lessonStartTimes = await groupsRepository.GetLessonStartTimesById(cancellationToken);
        return new GroupOrdersSwitchNames(teachers, students, weekDays, lessonStartTimes);
    }

    private static SwitchToGroupOrdersResponse ToResponse(GroupOrdersSwitchPlan plan, bool dryRun, bool switched,
        DateTime transitionDate)
    {
        return new SwitchToGroupOrdersResponse(dryRun, switched, transitionDate,
        [
            .. plan.Violations.Select(v => new GroupOrdersSwitchViolationResponse(v.GroupId, v.GroupCode, v.Rule.Code,
                v.Rule.Description, v.Date, v.Row))
        ],
        [
            .. plan.Groups.Select(g =>
                new GroupOrdersSwitchGroupResponse(g.GroupId, g.GroupCode, g.InitialOrder?.OrderDate))
        ]);
    }
}
