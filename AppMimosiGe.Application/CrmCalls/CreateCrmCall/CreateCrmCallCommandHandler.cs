using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.Validation;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.CreateCrmCall;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateCrmCallCommandHandler(ICrmCallsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCrmCallCommand, int>
{
    //ზარი ბალანსს და შემდეგი გადახდის თარიღს არ ცვლის, ამიტომ dirty ალამი არ ერთვება: "უნდა გადაიხადოს"
    //ბალანსების გვერდზე ყოველ ჩატვირთვაზე ბაზიდან იკითხება
    public async Task<Result<int>> Handle(CreateCrmCallCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        var crmCall = new CrmCall();
        CrmCallMapper.ApplyFields(crmCall, command.Request!);
        repository.Add(crmCall);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return crmCall.CcId;
    }
}
