using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.UpdateSalaryHeader;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateSalaryHeaderCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateSalaryHeaderCommand>
{
    //თარიღის შეცვლა გამოთვლილ სტრიქონებს არ ცვლის: ახალი თარიღით ხელახლა გამოთვლაა საჭირო (Access-იც ასე იყო)
    public async Task<Result> Handle(UpdateSalaryHeaderCommand command, CancellationToken cancellationToken)
    {
        SalaryHeader? header = await repository.GetHeaderForChange(command.ShId, cancellationToken);
        if (header is null)
        {
            return Result.Failure(SalaryErrors.SalaryHeaderNotFound);
        }

        header.ShChargeDate = command.Request!.ShChargeDate.Date;
        header.ShTransferDate = command.Request.ShTransferDate.Date;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
