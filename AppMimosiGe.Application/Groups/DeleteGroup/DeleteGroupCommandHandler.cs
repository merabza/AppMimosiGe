using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.DeleteGroup;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteGroupCommandHandler(IGroupsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteGroupCommand>
{
    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        Group? group = await repository.GetForChange(command.GrpId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(GroupErrors.GroupNotFound);
        }

        //გაკვეთილებიანი ჯგუფი არ იშლება, ის გაუქმების თარიღით უქმდება (D57)
        if (await repository.IsInUse(command.GrpId, cancellationToken))
        {
            return Result.Failure(GroupErrors.GroupIsInUse);
        }

        //Access-ში ჯგუფის წაშლა ყველა კონტრაქტს ნიშნავდა (SetAllNextPayDateDirty); აქ მხოლოდ ამ ჯგუფის მოსწავლეებს
        await repository.MarkNextPayDatesDirty(group.GroupsByStudents.Select(x => x.StudentContractId),
            cancellationToken);
        repository.Remove(group);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
