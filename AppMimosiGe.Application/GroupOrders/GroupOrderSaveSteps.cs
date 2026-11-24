using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders;

/// <summary>
///     ბრძანების შექმნის, შეცვლისა და წაშლის საერთო ნაბიჯები (GROUP_ORDERS.md §5): რეჟიმი, თარიღი და უფლება,
///     გადათამაშება (D185), წესები სამიზნე მდგომარეობაზე და ჩაწერა. ცვლილებები ერთ კონტექსტში გროვდება და ერთი
///     SaveChanges-ით, ერთ ტრანზაქციაში იწერება. ბრძანება იმავე წესებს გადის, რასაც ჯგუფის რედაქტორის შენახვა
///     (UpdateGroupCommandHandler)
/// </summary>
internal sealed class GroupOrderSaveSteps(
    IGroupOrdersRepository repository,
    IGroupsRepository groupsRepository,
    IValidator<GroupRequest> groupValidator,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    IUserClaimRights claimRights,
    TimeProvider timeProvider)
{
    //ახალი ბრძანების Id გადათამაშებისას: ერთი დღის ბრძანებებში ის ბოლოა, როგორც შექმნის რიგით (D185)
    public const int NewOrderId = int.MaxValue;

    //ჯგუფის დროებითი Id, როცა შექმნის ბრძანება მას ახლა ქმნის; ბაზის ჯგუფებს დადებითი Id აქვს
    public const int NewGroupId = 0;

    /// <summary>
    ///     გადასვლის დღე (D0), ან შეცდომა, თუ ბრძანებების რეჟიმი ჩართული არ არის (D183)
    /// </summary>
    public async Task<Result<DateTime>> TransitionDate(CancellationToken cancellationToken)
    {
        GroupOrdersModeResponse mode = await repository.GetMode(cancellationToken);
        if (!mode.IsOrdersMode)
        {
            return GroupOrderErrors.GroupOrdersAreNotEnabled;
        }

        return mode.TransitionDate ??
               throw new InvalidOperationException("The group orders mode has no transition date");
    }

    /// <summary>
    ///     ბრძანება მოთხოვნიდან (GroupOrderRequestMapper): სქემის გარეშე მოსული მასწავლებლის ხაზი კონტრაქტის ძირითად
    ///     სქემას იღებს (D187)
    /// </summary>
    public async Task<GroupOrderData> ToData(GroupOrderRequest request, int orderId, int? groupId,
        CancellationToken cancellationToken)
    {
        Dictionary<int, int> defaultSalarySchemes = await groupsRepository.GetDefaultSalarySchemes(
            request.Teachers.Where(x => x.SalarySchemaId is null).Select(x => x.TeacherContractId), cancellationToken);
        return GroupOrderRequestMapper.ToData(request, orderId, groupId, defaultSalarySchemes);
    }

    /// <summary>
    ///     თარიღისა და უფლების წესი (GroupOrderDateRule: D184, D196, D210, D211): Manager-ს შეუძლია a ≥ დღეს, წარსულს და
    ///     საწყისი ბრძანების შეცვლას AppClaim ChangePastGroupOrders სჭირდება
    /// </summary>
    public async Task<Error?> CheckDate(GroupOrderData? before, GroupOrderData? after,
        CancellationToken cancellationToken)
    {
        bool canChangePastOrders =
            await claimRights.HasClaim(GroupOrderClaims.ChangePastGroupOrders, cancellationToken);
        return GroupOrderDateRule.Check(before, after, Today(), canChangePastOrders);
    }

    /// <summary>
    ///     შექმნის ან შეცვლის შედეგის ჩაწერა order-ში და შეხებულ ჯგუფებში: კოდი (D186), დაპაუზება (D186, D213), D202,
    ///     გადათამაშება (D185), წესები სამიზნე მდგომარეობაზე და ჩაწერა (C1). before: ბრძანება ცვლილებამდე (null:
    ///     ახალი), after: ცვლილების შემდეგ. აბრუნებს შეცდომას, ან null-ს; ბაზაში არაფერი იწერება
    /// </summary>
    public async Task<Error?> Save(GroupOrder order, GroupOrderData? before, GroupOrderData after,
        DateTime transitionDate, CancellationToken cancellationToken)
    {
        if (after.Fields is { } fields && await repository.GroupCodeIsTaken(fields.AcademicYearId, fields.GroupCode,
                before?.GroupId, before?.Id, cancellationToken))
        {
            return GroupOrderErrors.GroupCodeIsTaken;
        }

        if (after.IsPaused)
        {
            return await SavePaused(order, before, after, cancellationToken);
        }

        List<GroupOrderData> orders = await repository.GetLinkedOrders(GroupIds(before, after), cancellationToken);
        if (GroupCanHaveOrdersRule.Check(after, orders) is { } cannotHaveOrders)
        {
            return cannotHaveOrders;
        }

        Result<GroupOrdersReplayResult> replay = await Replay(before, after, orders, cancellationToken);
        if (replay.IsFailure)
        {
            return replay.Error;
        }

        if (await CheckRules(replay.Value.States, transitionDate, cancellationToken) is { } ruleError)
        {
            return ruleError;
        }

        await Write(order, before, after, replay.Value.States, cancellationToken);
        return null;
    }

    /// <summary>
    ///     ბრძანების წაშლა (D191): დაპაუზებული უბრალოდ იშლება (D211), შექმნის ბრძანება ჯგუფს შლის, სხვა ბრძანება კი
    ///     გადათამაშებით იშლება, წესების შემოწმებით. საწყისი ბრძანება აქ არ მოდის: ის არ იშლება (D196, CheckDate)
    /// </summary>
    public async Task<Error?> Delete(GroupOrder order, GroupOrderData before, DateTime transitionDate,
        CancellationToken cancellationToken)
    {
        if (before.IsPaused)
        {
            repository.RemoveOrder(order);
            return null;
        }

        if (before.OrderType == EGroupOrderType.CreateGroup)
        {
            return await DeleteGroup(before.GroupId!.Value, null, cancellationToken);
        }

        List<GroupOrderData> orders = await repository.GetLinkedOrders(GroupIds(before, null), cancellationToken);
        Result<GroupOrdersReplayResult> replay = await Replay(before, null, orders, cancellationToken);
        if (replay.IsFailure)
        {
            return replay.Error;
        }

        if (await CheckRules(replay.Value.States, transitionDate, cancellationToken) is { } ruleError)
        {
            return ruleError;
        }

        await repository.WriteStates(replay.Value.States, GroupOrderNewLines.None, null, cancellationToken);
        repository.RemoveOrder(order);
        return null;
    }

    /// <summary>
    ///     ერთი SaveChanges, ერთი ტრანზაქცია. ერთდროული შენახვისას (ნომრის მთვლელი, უკვე წაშლილი ბრძანება) მეორე შენახვა
    ///     მთლიანად უარყოფილია და არაფერი იწერება (D214)
    /// </summary>
    public async Task<Error?> SaveChanges(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return GroupOrderErrors.OrderSavedConcurrently;
        }
    }

    //დაპაუზებული შექმნის ბრძანება ჯგუფს არ ქმნის (D186); მისი ხაზების საათები საკუთარი განრიგით ითვლება. თუ ის
    //გააქტიურებული იყო, ჯგუფი იშლება, როგორც ამ ბრძანების წაშლისას (D213, D191)
    private async Task<Error?> SavePaused(GroupOrder order, GroupOrderData? before, GroupOrderData after,
        CancellationToken cancellationToken)
    {
        if (before is { IsPaused: false, GroupId: { } groupId } &&
            await DeleteGroup(groupId, order.Id, cancellationToken) is { } deleteError)
        {
            return deleteError;
        }

        (_, GroupOrderRemovedLines removedLines) = GroupOrderEntityMapper.Write(order, after,
            [.. after.DayTimePlaces.Select(d => d.ToRow(after.OrderDate, null))]);
        repository.RemoveLines(removedLines);
        order.AcademicYearId = after.Fields!.AcademicYearId;
        return null;
    }

    //D191: ჯგუფი იშლება, თუ მას გადაყვანა სხვა ჯგუფთან არ აკავშირებს და მისი გაკვეთილები ხელფასის დათვლაში არ არის.
    //keptOrderId: ხელახლა დაპაუზებული შექმნის ბრძანება რჩება (D213)
    private async Task<Error?> DeleteGroup(int groupId, int? keptOrderId, CancellationToken cancellationToken)
    {
        List<GroupOrderData> orders = await repository.GetOrders([groupId], cancellationToken);
        if (orders.Exists(o => o is { OrderType: EGroupOrderType.TransferStudents, IsPaused: false }))
        {
            return GroupOrderErrors.CreateOrderHasTransfers;
        }

        Result deleted = await repository.DeleteGroup(groupId, keptOrderId, cancellationToken);
        return deleted.IsFailure ? deleted.Error : null;
    }

    //D185: თუ შესანახი ბრძანება არ ესადაგება, ბრუნდება მიზეზი; თუ გვიანდელი, მისი ნომერი
    private async Task<Result<GroupOrdersReplayResult>> Replay(GroupOrderData? before, GroupOrderData? after,
        List<GroupOrderData> orders, CancellationToken cancellationToken)
    {
        GroupOrdersReplayResult result = GroupOrdersReplay.Run(before, after, orders);
        if (result.NotFitting is not { } notFitting)
        {
            return result;
        }

        if (notFitting.OrderId == after?.Id)
        {
            return notFitting.Error;
        }

        //გვიანდელი ბრძანება საწყისი ვერ იქნება: ის ჯგუფის პირველი ბრძანებაა
        OrderNumber number = await repository.GetNumber(notFitting.OrderId, cancellationToken) ??
                             throw new InvalidOperationException($"Group order {notFitting.OrderId} has no number");
        return GroupOrderErrors.OrderNoLongerFits(number.ToString());
    }

    //წესები სამიზნე მდგომარეობაზე (§5 ნაბიჯი 4): ჯგუფის მოთხოვნის ვალიდატორები, ფორმულები D0-დან (D187, D194),
    //დროების გადაფარვა D0-დან (D178, D179, D212) და ახალი შეცდომიანი გაკვეთილები (D177)
    private async Task<Error?> CheckRules(IReadOnlyDictionary<int, GroupState> states, DateTime transitionDate,
        CancellationToken cancellationToken)
    {
        if ((await ValidateGroups(states, cancellationToken) ??
             await FindFormulasError(states, transitionDate, cancellationToken)) is { } error)
        {
            return error;
        }

        Dictionary<int, TimeOnly> lessonStartTimes = await groupsRepository.GetLessonStartTimesById(cancellationToken);
        return await FindTimesOverlap(states, lessonStartTimes, transitionDate, cancellationToken) ??
               await FindNewLessonErrors(states, lessonStartTimes, cancellationToken);
    }

    //ჯგუფის მოთხოვნის ვალიდატორები (GroupRequestValidator: D55, D181-ის 00:00, ცნობარები), როგორც რედაქტორში;
    //შეცდომები ისევე ბრუნდება, როგორც ValidationDecorator-იდან
    private async Task<Error?> ValidateGroups(IReadOnlyDictionary<int, GroupState> states,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];
        foreach (GroupState state in states.Values)
        {
            ValidationResult result =
                await groupValidator.ValidateAsync(GroupStateMapper.ToGroupRequest(state), cancellationToken);
            failures.AddRange(result.Errors);
        }

        return failures.Count == 0
            ? null
            : new ValidationError([.. failures.Select(f => Error.Problem(f.ErrorCode, f.ErrorMessage))]);
    }

    //საათებისა და ფასის ფორმულები გადასვლის დღიდან (D187, D194); აღწერაში მოსწავლის სახელია
    private async Task<Error?> FindFormulasError(IReadOnlyDictionary<int, GroupState> states, DateTime transitionDate,
        CancellationToken cancellationToken)
    {
        List<GroupOrderFormulaViolation> violations =
        [
            .. states.Values.SelectMany(s => GroupOrderFormulasRule.Find(s, transitionDate)).OrderBy(v => v.Date)
        ];
        if (violations.Count == 0)
        {
            return null;
        }

        Dictionary<int, string> studentNames = await groupsRepository.GetStudentContractNames(
            [.. violations.Select(v => v.Row.StudentContractId).Distinct()], cancellationToken);
        return GroupOrderFormulasRule.ToError(violations, v => v.Describe(studentNames[v.Row.StudentContractId]));
    }

    //D178, D179: შეხებული ჯგუფები ერთმანეთსაც ედრება, მათი ბაზის განრიგის ნაცვლად სამიზნე მდგომარეობით. ჯერ
    //მასწავლებლები, მერე მოსწავლეები, როგორც რედაქტორში. გადასვლის დღემდე დასრულებული გადაფარვა არ ბლოკავს (D212)
    private async Task<Error?> FindTimesOverlap(IReadOnlyDictionary<int, GroupState> states,
        IReadOnlyDictionary<int, TimeOnly> lessonStartTimes, DateTime transitionDate,
        CancellationToken cancellationToken)
    {
        List<GroupSchedule> teacherSchedules =
            [.. states.Select(s => GroupStateMapper.ToTeacherSchedule(s.Key, s.Value, lessonStartTimes))];
        foreach (GroupSchedule schedule in teacherSchedules)
        {
            if (await groupsRepository.FindTeacherTimesOverlap(schedule, transitionDate, teacherSchedules,
                    cancellationToken) is { } error)
            {
                return error;
            }
        }

        List<GroupSchedule> studentSchedules =
            [.. states.Select(s => GroupStateMapper.ToStudentSchedule(s.Key, s.Value, lessonStartTimes))];
        foreach (GroupSchedule schedule in studentSchedules)
        {
            if (await groupsRepository.FindStudentTimesOverlap(schedule, transitionDate, studentSchedules,
                    cancellationToken) is { } error)
            {
                return error;
            }
        }

        return null;
    }

    //D177: ჩაწერა შეტანილი მონაცემის მქონე გაკვეთილს ან მოსწავლეს ზედმეტს არ ხდის (რეპორტი r13). ახალ ჯგუფს
    //გაკვეთილები ჯერ არ აქვს
    private async Task<Error?> FindNewLessonErrors(IReadOnlyDictionary<int, GroupState> states,
        IReadOnlyDictionary<int, TimeOnly> lessonStartTimes, CancellationToken cancellationToken)
    {
        int[] groupIds = [.. states.Keys.Where(id => id != NewGroupId)];
        if (groupIds.Length == 0)
        {
            return null;
        }

        DateTime now = timeProvider.GetLocalNow().DateTime;
        List<DateTime> lessonDates = [];
        foreach (Group group in await repository.GetGroupsForChange(groupIds, cancellationToken))
        {
            //ჩაწერა სტრიქონს ხაზით პოულობს და მის Id-ს ინარჩუნებს (GroupStateWriter)
            Dictionary<int, int> gbsIdsByLineId = group.GroupsByStudents.Where(s => s.GroupOrderStudentId is not null)
                .ToDictionary(s => s.GroupOrderStudentId!.Value, s => s.GbsId);
            GroupState state = states[group.GrpId];
            IReadOnlyList<ErroneousLessonStudent> errors = await GroupLessonsGeneration.FindNewLessonErrors(
                lessonGeneratorRepository, unitOfWork, group.GrpId, now,
                current => GroupStateMapper.ToLessonsInput(state, current, gbsIdsByLineId, lessonStartTimes),
                cancellationToken);
            lessonDates.AddRange(errors.Select(e => e.LessonDt));
        }

        return lessonDates.Count == 0
            ? null
            : GroupErrors.ChangeCreatesLessonsWithErrors([.. lessonDates.Distinct().Order()]);
    }

    //ბრძანება და შეხებული ჯგუფები: ახალი ხაზები სტრიქონებს navigation-ით უკავშირდება, შექმნის ბრძანება კი ახალ
    //ჯგუფს. სასწავლო წელი ჯგუფისაა; თუ საწყის ან შექმნის ბრძანებაში შეიცვალა, ჯგუფის სხვა ბრძანებებიც იცვლება
    private async Task Write(GroupOrder order, GroupOrderData? before, GroupOrderData after,
        IReadOnlyDictionary<int, GroupState> states, CancellationToken cancellationToken)
    {
        int groupId = after.GroupId!.Value;
        (GroupOrderNewLines newLines, GroupOrderRemovedLines removedLines) = GroupOrderEntityMapper.Write(order, after,
            states[after.TargetGroupId ?? groupId].DayTimePlaces);
        repository.RemoveLines(removedLines);

        bool createsGroup = groupId == NewGroupId;
        IReadOnlyDictionary<int, Group> groups = await repository.WriteStates(states, newLines,
            createsGroup ? NewGroupId : null, cancellationToken);
        int academicYearId = states[groupId].Fields.AcademicYearId;
        order.AcademicYearId = academicYearId;
        if (createsGroup)
        {
            order.GroupId = null;
            order.Group = groups[NewGroupId];
        }
        else if (before?.Fields is { } beforeFields && beforeFields.AcademicYearId != academicYearId)
        {
            await repository.SetOrdersAcademicYear(groupId, academicYearId, cancellationToken);
        }
    }

    //ცვლილებამდელი და შემდგომი ბრძანების ჯგუფები ბაზიდან, ახალი ჯგუფის დროებითი Id-ის გარეშე
    private static IEnumerable<int> GroupIds(GroupOrderData? before, GroupOrderData? after)
    {
        return new[] { before?.GroupId, before?.TargetGroupId, after?.GroupId, after?.TargetGroupId }.OfType<int>()
            .Where(id => id != NewGroupId).Distinct();
    }

    private DateTime Today()
    {
        return timeProvider.GetLocalNow().DateTime.Date;
    }
}
