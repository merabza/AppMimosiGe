using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.DeleteGroupOrder;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteGroupOrderCommandHandler(
    IGroupOrdersRepository repository,
    IGroupsRepository groupsRepository,
    IValidator<GroupRequest> groupValidator,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    IUserClaimRights claimRights,
    TimeProvider timeProvider) : ICommandHandler<DeleteGroupOrderCommand>
{
    private readonly GroupOrderSaveSteps _steps = new(repository, groupsRepository, groupValidator,
        lessonGeneratorRepository, unitOfWork, claimRights, timeProvider);

    //ბრძანება იშლება იმავე უფლებით, რითაც იცვლება, და იშლება გადათამაშებით (D191)
    public async Task<Result> Handle(DeleteGroupOrderCommand command, CancellationToken cancellationToken)
    {
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

        GroupOrderData before = GroupOrderEntityMapper.ToData(order);
        if (await _steps.CheckDate(before, null, cancellationToken) is { } dateError)
        {
            return dateError;
        }

        if (await _steps.Delete(order, before, transitionDate.Value, cancellationToken) is { } deleteError)
        {
            return deleteError;
        }

        return await _steps.SaveChanges(cancellationToken) is { } concurrencyError
            ? concurrencyError
            : Result.Success();
    }
}
