using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.SwitchToGroupEditing;

/// <summary>
///     ვიზარდი 2 (D195): ჯერ ყველა ბრძანება ხაზებით ფაილში იწერება (GroupOrders:ExportFolder), მერე ერთ ტრანზაქციაში
///     იშლება სტრიქონების კავშირები ხაზებთან და ბრძანებები, და რეჟიმი ჯგუფებისა ხდება. თუ ფაილი ვერ ჩაიწერა, არაფერი
///     იშლება. ჯგუფებს, სტრიქონებს, გაკვეთილებს და ნომრების მთვლელს ვიზარდი არ ეხება: ნომერი თავიდან არ გაიცემა (D192)
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SwitchToGroupEditingCommandHandler(
    IGroupOrdersRepository repository,
    IGroupOrdersExportFiles exportFiles,
    GroupOrdersSettings settings,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<SwitchToGroupEditingCommand, SwitchToGroupEditingResponse>
{
    public async Task<Result<SwitchToGroupEditingResponse>> Handle(SwitchToGroupEditingCommand command,
        CancellationToken cancellationToken)
    {
        GroupOrdersMode? mode = await repository.GetModeForChange(cancellationToken);
        if (mode is not { IsOrdersMode: true })
        {
            return GroupOrderErrors.GroupOrdersAreAlreadyDisabled;
        }

        if (string.IsNullOrWhiteSpace(settings.ExportFolder))
        {
            return GroupOrderErrors.ExportFolderIsNotConfigured;
        }

        DateTime now = timeProvider.GetLocalNow().DateTime;
        List<GroupOrder> orders = await repository.GetAllOrdersForChange(cancellationToken);
        GroupOrderLinkedRows linkedRows = await repository.GetLinkedRowsForChange(cancellationToken);
        GroupOrdersExportFile file = GroupOrdersExportFile.Create(
            GroupOrdersExportMapper.ToExport(orders, linkedRows, mode.TransitionDate, currentUser.Name, now));
        if (!await exportFiles.TryWrite(file.FileName, file.Content, cancellationToken))
        {
            return GroupOrderErrors.ExportFileWriteFailed;
        }

        GroupOrdersSwitchWriter.Unlink(linkedRows);
        foreach (GroupOrder order in orders)
        {
            repository.RemoveOrder(order);
        }

        mode.IsOrdersMode = false;
        mode.TransitionDate = null;
        mode.SwitchedByUserName = currentUser.Name;
        mode.SwitchedAt = now;

        //ერთი SaveChanges, ერთი ტრანზაქცია. ერთდროული გაშვებისას (რეჟიმის SwitchedAt, D221), ან თუ ამასობაში ბრძანება
        //წაიშალა, მთლიანად უარყოფილია; ჩაწერილი ფაილი რჩება
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return GroupOrderErrors.ModeSwitchedConcurrently;
        }

        return new SwitchToGroupEditingResponse(file.FileName, orders.Count);
    }
}
