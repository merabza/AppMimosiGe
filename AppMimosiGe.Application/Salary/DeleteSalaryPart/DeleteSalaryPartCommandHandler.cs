using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.DeleteSalaryPart;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteSalaryPartCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteSalaryPartCommand>
{
    //გამოთვლით შექმნილ მდგენელს (ტიპი 1) მხოლოდ ხელახალი გამოთვლა შლის
    public async Task<Result> Handle(DeleteSalaryPartCommand command, CancellationToken cancellationToken)
    {
        SalaryPart? part = await repository.GetPartForChange(command.SpId, cancellationToken);
        if (part is null)
        {
            return Result.Failure(SalaryErrors.SalaryPartNotFound);
        }

        if (part.SalaryPartTypeId == SalaryCalculator.LessonSalaryPartTypeId)
        {
            return Result.Failure(SalaryErrors.PartIsCalculated);
        }

        repository.RemovePart(part);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
