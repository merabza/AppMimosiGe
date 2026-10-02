using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.GetWorkHourFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetWorkHourFormLookupsQueryHandler(IWorkHoursRepository repository)
    : IQueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse>
{
    //Access-ის ჩამოსაშლელი სია: ყველა სამუშაო საათების ჯგუფის მქონე კონტრაქტი, დასრულებულიც (ძველი ჩანაწერების
    //ფილტრისთვის)
    public async Task<Result<WorkHourFormLookupsResponse>> Handle(GetWorkHourFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        return new WorkHourFormLookupsResponse(await repository.GetEmployeeLookups(cancellationToken));
    }
}
