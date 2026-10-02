using System.Threading;
using System.Threading.Tasks;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.CreateSalaryHeader;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateSalaryHeaderCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSalaryHeaderCommand, int>
{
    public async Task<Result<int>> Handle(CreateSalaryHeaderCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        var header = new SalaryHeader
        {
            ShChargeDate = command.Request!.ShChargeDate.Date, ShTransferDate = command.Request.ShTransferDate.Date
        };
        repository.AddHeader(header);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return header.ShId;
    }
}
