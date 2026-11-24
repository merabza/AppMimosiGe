using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrderFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupOrderFormLookupsQueryHandler(
    IGroupOrdersRepository repository,
    IGroupsRepository groupsRepository,
    IStudentContractsRepository studentContractsRepository,
    ITeacherContractsRepository teacherContractsRepository,
    TimeProvider timeProvider) : IQueryHandler<GetGroupOrderFormLookupsQuery, GroupOrderFormLookupsResponse>
{
    //პარამეტრები აქ მოწმდება, რადგან ValidationDecorator მხოლოდ command-ებს ამოწმებს. სიები იგივეა (და იგივენაირად
    //ლაგდება), რაც ჯგუფის ფორმაში
    public async Task<Result<GroupOrderFormLookupsResponse>> Handle(GetGroupOrderFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await studentContractsRepository.GetAcademicYears(cancellationToken);
        List<LookupItemResponse> courses = await studentContractsRepository.GetCourses(cancellationToken);
        List<LookupItemResponse> groupSizes = await studentContractsRepository.GetGroupSizes(cancellationToken);
        if (CheckLookupIds(request, academicYears, courses, groupSizes) is { } lookupError)
        {
            return lookupError;
        }

        GroupOrderData? editedOrder = null;
        if (request.OrderId is { } orderId)
        {
            editedOrder = await repository.GetOrder(orderId, cancellationToken);
            if (editedOrder is null)
            {
                return GroupOrderErrors.GroupOrderNotFound;
            }
        }

        Result<string?> suggestedGroupCode = await SuggestGroupCode(request, editedOrder, cancellationToken);
        if (suggestedGroupCode.IsFailure)
        {
            return suggestedGroupCode.Error;
        }

        Result<RequestedGroups> requestedGroups = await LoadGroups(request, editedOrder, cancellationToken);
        if (requestedGroups.IsFailure)
        {
            return requestedGroups.Error;
        }

        List<GroupOrderGroupLookupResponse> groups = [];
        List<GroupStudentContractLookupResponse> studentContracts = [];
        if (request.AcademicYearId is { } academicYearId)
        {
            groups = await repository.GetGroupLookups(academicYearId, cancellationToken);
            studentContracts = await groupsRepository.GetStudentContracts(academicYearId, cancellationToken);
        }

        RequestedGroups requested = requestedGroups.Value;
        Dictionary<int, string> studentNames = await StudentNames(requested, cancellationToken);
        DateTime today = timeProvider.GetLocalNow().Date;
        return new GroupOrderFormLookupsResponse(CurrentAcademicYear.Find(academicYears, today), [
                .. academicYears.OrderBy(ay => ay.AcademicYearName, StringComparer.Ordinal)
                    .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
            ], courses, groupSizes, await studentContractsRepository.GetStudentStatuses(cancellationToken),
            await groupsRepository.GetTeacherContracts(cancellationToken),
            await teacherContractsRepository.GetSalarySchemes(cancellationToken),
            await groupsRepository.GetWeekDays(cancellationToken),
            await groupsRepository.GetLessonStartTimes(cancellationToken),
            await groupsRepository.GetRooms(cancellationToken), groups, studentContracts,
            StudentTariffs(request, studentContracts, requested), StateResponse(requested.Group, requested, studentNames),
            StateResponse(requested.Target, requested, studentNames), suggestedGroupCode.Value);
    }

    private static Error? CheckLookupIds(GetGroupOrderFormLookupsQuery request, List<AcademicYear> academicYears,
        List<LookupItemResponse> courses, List<LookupItemResponse> groupSizes)
    {
        if (request.AcademicYearId is { } academicYearId && !academicYears.Exists(ay => ay.AyId == academicYearId))
        {
            return GroupErrors.AcademicYearNotFound;
        }

        if (request.CourseId is { } courseId && !courses.Exists(c => c.Id == courseId))
        {
            return GroupErrors.CourseNotFound;
        }

        return request.GroupSizeId is { } groupSizeId && !groupSizes.Exists(s => s.Id == groupSizeId)
            ? GroupErrors.GroupSizeNotFound
            : null;
    }

    //D186: სტატუსის კლასისა და წლის პირველი თავისუფალი კოდი. შესაცვლელი საწყისი ან შექმნის ბრძანების კოდი და მისი
    //ჯგუფის კოდი თავისუფლად ითვლება, როგორც შენახვისას (GroupCodeIsTaken)
    private async Task<Result<string?>> SuggestGroupCode(GetGroupOrderFormLookupsQuery request,
        GroupOrderData? editedOrder, CancellationToken cancellationToken)
    {
        if (request.StudentStatusId is not { } studentStatusId)
        {
            return Result.Success<string?>(null);
        }

        if (await repository.GetStudentStatusRate(studentStatusId, cancellationToken) is not { } rate)
        {
            return Result.Failure<string?>(GroupErrors.StudentStatusNotFound);
        }

        if (request.AcademicYearId is not { } academicYearId)
        {
            return Result.Success<string?>(null);
        }

        int? exceptGrpId = editedOrder is { OrderType: EGroupOrderType.Initial or EGroupOrderType.CreateGroup }
            ? editedOrder.GroupId
            : null;
        List<string> takenCodes = await repository.GetTakenGroupCodes(academicYearId, exceptGrpId, editedOrder?.Id,
            cancellationToken);
        return Result.Success(GroupCodeSuggestion.Suggest(rate, takenCodes));
    }

    //ჯგუფების მდგომარეობა ბრძანების თარიღზე, ბრძანებამდე (GroupStatesBeforeOrder). ბრძანებების გარეშე ჯგუფისა ბაზის
    //სტრიქონებიდან მოდის; ჯგუფი, რომელიც ამ ადგილას ჯერ არ შექმნილა, სტრიქონების გარეშეა. თუ შესაცვლელი ბრძანების
    //გარეშე უფრო ადრეული ბრძანება აღარ ესადაგება, შეცდომა იგივეა, რაც შენახვისას (D185)
    private async Task<Result<RequestedGroups>> LoadGroups(GetGroupOrderFormLookupsQuery request,
        GroupOrderData? editedOrder, CancellationToken cancellationToken)
    {
        int[] groupIds = [.. new[] { request.GrpId, request.TargetGrpId }.OfType<int>().Distinct()];
        if (groupIds.Length == 0)
        {
            return new RequestedGroups(default, null, null);
        }

        if (request.OrderDate is not { } requestedOrderDate)
        {
            return GroupOrderErrors.OrderDateIsRequired;
        }

        DateTime orderDate = requestedOrderDate.Date;
        Dictionary<int, GroupState> storedStates = await repository.GetGroupStates(groupIds, cancellationToken);
        if (request.GrpId is { } grpId && !storedStates.ContainsKey(grpId))
        {
            return GroupOrderErrors.GroupNotFound;
        }

        if (request.TargetGrpId is { } targetGrpId && !storedStates.ContainsKey(targetGrpId))
        {
            return GroupOrderErrors.TargetGroupNotFound;
        }

        List<GroupOrderData> orders = await repository.GetLinkedOrders(groupIds, cancellationToken);
        GroupOrdersFoldResult fold = GroupStatesBeforeOrder.Run(orders, orderDate,
            editedOrder?.Id ?? GroupOrderSaveSteps.NewOrderId);
        if (fold.NotFitting is { } notFitting)
        {
            OrderNumber number = await repository.GetNumber(notFitting.OrderId, cancellationToken) ??
                                 throw new InvalidOperationException(
                                     $"Group order {notFitting.OrderId} has no number");
            return GroupOrderErrors.OrderNoLongerFits(number.ToString());
        }

        return new RequestedGroups(orderDate, Load(request.GrpId), Load(request.TargetGrpId));

        RequestedGroup? Load(int? groupId)
        {
            if (groupId is not { } id)
            {
                return null;
            }

            DateTime? startDate = orders.Find(o =>
                    o.GroupId == id && o.OrderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup)
                ?.OrderDate;
            if (fold.States.TryGetValue(id, out GroupState? foldedState))
            {
                return new RequestedGroup(id, foldedState, startDate);
            }

            GroupState storedState = storedStates[id];
            return new RequestedGroup(id,
                startDate is null
                    ? storedState
                    : storedState with { Teachers = [], Students = [], DayTimePlaces = [] }, startDate);
        }
    }

    private async Task<Dictionary<int, string>> StudentNames(RequestedGroups requested,
        CancellationToken cancellationToken)
    {
        int[] studentContractIds =
        [
            .. new[] { requested.Group, requested.Target }.OfType<RequestedGroup>()
                .SelectMany(g => GroupOrderFormLookups.ActiveStudents(g.State, requested.OrderDate))
                .Select(s => s.StudentContractId).Distinct()
        ];
        return studentContractIds.Length == 0
            ? []
            : await groupsRepository.GetStudentContractNames(studentContractIds, cancellationToken);
    }

    private static GroupOrderGroupStateResponse? StateResponse(RequestedGroup? group, RequestedGroups requested,
        IReadOnlyDictionary<int, string> studentNames)
    {
        return group is null
            ? null
            : GroupOrderFormLookups.ToGroupState(group.GrpId, group.State, requested.OrderDate, group.StartDate,
                studentNames);
    }

    //D187, D218: გადაყვანისას სამიზნე ჯგუფის საგანი და ზომა „საიდან" ჯგუფის სტრიქონებით, სხვა ჯგუფის ბრძანებაში ჯგუფისა,
    //შექმნის ბრძანებაში კი მოთხოვნისა
    private static List<GroupOrderStudentTariffResponse> StudentTariffs(GetGroupOrderFormLookupsQuery request,
        List<GroupStudentContractLookupResponse> studentContracts, RequestedGroups requested)
    {
        if (requested.Target is { } target)
        {
            return GroupOrderFormLookups.TransferStudentTariffs(studentContracts, target.State.Fields,
                requested.Group is { } source
                    ? GroupOrderFormLookups.ActiveStudents(source.State, requested.OrderDate)
                    : []);
        }

        if (requested.Group is { } group)
        {
            return GroupOrderFormLookups.StudentTariffs(studentContracts, group.State.Fields.CourseId,
                group.State.Fields.GroupSizeId);
        }

        return request is { CourseId: { } courseId, GroupSizeId: { } groupSizeId }
            ? GroupOrderFormLookups.StudentTariffs(studentContracts, courseId, groupSizeId)
            : [];
    }

    //მოთხოვნილი ჯგუფი: მდგომარეობა ბრძანებამდე და დაწყება (საწყისი ან შექმნის ბრძანების თარიღი, D202)
    private sealed record RequestedGroup(int GrpId, GroupState State, DateTime? StartDate);

    private sealed record RequestedGroups(DateTime OrderDate, RequestedGroup? Group, RequestedGroup? Target);
}
