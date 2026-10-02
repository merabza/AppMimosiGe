using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.GetWorkHour;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetWorkHourQueryHandler(IWorkHoursRepository repository)
    : IQueryHandler<GetWorkHourQuery, WorkHourResponse>
{
    public async Task<Result<WorkHourResponse>> Handle(GetWorkHourQuery request, CancellationToken cancellationToken)
    {
        WorkHourResponse? workHour = await repository.GetOne(request.WhId, cancellationToken);
        return workHour is null ? WorkHourErrors.WorkHourNotFound : workHour;
    }
}
