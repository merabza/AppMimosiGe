using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcass.Application.Identity;
using FluentValidation;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.UpdateGroupOrder;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateGroupOrderCommandHandler(
    IGroupOrdersRepository repository,
    IGroupsRepository groupsRepository,
    IValidator<GroupRequest> groupValidator,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    IUserClaimRights claimRights,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<UpdateGroupOrderCommand>
{
    private readonly GroupOrderSaveSteps _steps = new(repository, groupsRepository, groupValidator,
        lessonGeneratorRepository, unitOfWork, claimRights, timeProvider);

    public async Task<Result> Handle(UpdateGroupOrderCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას და ტიპის ნაწილებს უკვე ამოწმებს
        GroupOrderRequest request = command.Request!;

        Result<DateTime> transitionDate = await _steps.TransitionDate(cancellationToken);
        if (transitionDate.IsFailure)
        {
            return transitionDate.Error;
        }

        GroupOrder? order = await repository.GetOrderForChange(command.OrderId, cancellationToken);
        if (order is null)
        {
            return GroupOrderErrors.GroupOrderNotFound;
        }

        //ბრძანების ტიპი არ იცვლება: სხვა ტიპის ცვლილება სხვა ბრძანებაა
        if ((int)order.OrderType != request.OrderType)
        {
            return GroupOrderErrors.OrderTypeIsInvalid;
        }

        if (!GroupOrderRequestMapper.LinesBelongToOrder(request, order))
        {
            return GroupOrderErrors.LineNotFound;
        }

        GroupOrderData before = GroupOrderEntityMapper.ToData(order);
        GroupOrderData after =
            await _steps.ToData(request, order.Id, GroupId(request, before), cancellationToken);
        if (await _steps.CheckDate(before, after, cancellationToken) is { } dateError)
        {
            return dateError;
        }

        if (await _steps.Save(order, before, after, transitionDate.Value, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        order.ChangedByUserName = currentUser.Name;
        order.ChangedAt = timeProvider.GetLocalNow().DateTime;
        return await _steps.SaveChanges(cancellationToken) is { } concurrencyError
            ? concurrencyError
            : Result.Success();
    }

    //საწყისი და შექმნის ბრძანების ჯგუფი ბრძანებისაა: დაპაუზებული შექმნის ბრძანება გააქტიურებისას ჯგუფს ქმნის (D186),
    //გააქტიურებული კი ხელახლა დაპაუზებისას შლის (D213)
    private static int? GroupId(GroupOrderRequest request, GroupOrderData before)
    {
        return before.OrderType switch
        {
            EGroupOrderType.Initial => before.GroupId,
            EGroupOrderType.CreateGroup when GroupOrderRequestMapper.IsPaused(request) => null,
            EGroupOrderType.CreateGroup => before.GroupId ?? GroupOrderSaveSteps.NewGroupId,
            _ => request.GroupId
        };
    }
}
