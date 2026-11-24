using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcass.Application.Identity;
using FluentValidation;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.CreateGroupOrder;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateGroupOrderCommandHandler(
    IGroupOrdersRepository repository,
    IGroupsRepository groupsRepository,
    IStudentContractsRepository studentContractsRepository,
    IValidator<GroupRequest> groupValidator,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    IUserClaimRights claimRights,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    GroupOrdersSettings settings) : ICommandHandler<CreateGroupOrderCommand, int>
{
    private readonly GroupOrderSaveSteps _steps = new(repository, groupsRepository, groupValidator,
        lessonGeneratorRepository, unitOfWork, claimRights, timeProvider);

    public async Task<Result<int>> Handle(CreateGroupOrderCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას და ტიპის ნაწილებს უკვე ამოწმებს
        GroupOrderRequest request = command.Request!;

        Result<DateTime> transitionDate = await _steps.TransitionDate(cancellationToken);
        if (transitionDate.IsFailure)
        {
            return transitionDate.Error;
        }

        //ახალი ბრძანების ყველა ხაზი ახალია (Id 0)
        if (!GroupOrderRequestMapper.LinesBelongToOrder(request, null))
        {
            return GroupOrderErrors.LineNotFound;
        }

        GroupOrderData after = await _steps.ToData(request, GroupOrderSaveSteps.NewOrderId, GroupId(request),
            cancellationToken);
        if (await _steps.CheckDate(null, after, cancellationToken) is { } dateError)
        {
            return dateError;
        }

        DateTime now = timeProvider.GetLocalNow().DateTime;
        var order = new GroupOrder { CreatedByUserName = currentUser.Name, CreatedAt = now };
        repository.AddOrder(order);
        if (await _steps.Save(order, null, after, transitionDate.Value, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        //ნომერი და გამოცემის თარიღი პირველი შენახვისას ენიჭება, დაპაუზებულ ბრძანებასაც (D192)
        AcademicYear academicYear =
            await studentContractsRepository.GetAcademicYear(order.AcademicYearId, cancellationToken) ??
            throw new InvalidOperationException($"Academic year {order.AcademicYearId} not found");
        await repository.Issue(order, academicYear.StartDate, now, settings, cancellationToken);

        if (await _steps.SaveChanges(cancellationToken) is { } concurrencyError)
        {
            return concurrencyError;
        }

        return order.Id;
    }

    //შექმნის ბრძანება ჯგუფს თვითონ ქმნის, თუ დაპაუზებული არ არის (D186); საწყისი ბრძანება ხელით არ იქმნება
    //(CheckDate)
    private static int? GroupId(GroupOrderRequest request)
    {
        if (request.OrderType != (int)EGroupOrderType.CreateGroup)
        {
            return request.GroupId;
        }

        return GroupOrderRequestMapper.IsPaused(request) ? null : GroupOrderSaveSteps.NewGroupId;
    }
}
