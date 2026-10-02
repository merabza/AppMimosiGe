using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.EndWork;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class EndWorkCommandHandler(
    IWorkHoursRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<EndWorkCommand, WorkHourResponse>
{
    //Access-ის cmdFixWorkEnd: დღეს დაწყებული ჩანაწერი უნდა არსებობდეს, სხვა შემთხვევაში შეცდომაა; WhEnd = ახლა +
    //ლუფტი (უკვე დასრულებულ ჩანაწერსაც ცვლის, როგორც Access-ში). დღეს რამდენიმე ჩანაწერისას ბოლოს დაწყებული
    //იცვლება (Access-ის DLookup ნებისმიერს იღებდა)
    public async Task<Result<WorkHourResponse>> Handle(EndWorkCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ს, თანამშრომლის არჩევას და ლუფტს უკვე ამოწმებს
        WorkTimeFixRequest request = command.Request!;
        WorkHourEmployee? employee = await repository.GetEmployee(request.TeacherContractId!.Value, cancellationToken);
        if (employee is null)
        {
            return WorkHourErrors.EmployeeNotFound;
        }

        DateTime now = WorkTimeFixRules.Now(timeProvider);
        WorkHour? workHour = await repository.GetLastRecordOfDayForChange(employee.Id, now.Date, cancellationToken);
        if (workHour is null)
        {
            return WorkHourErrors.TodayRecordNotFound;
        }

        //დაწყება ხელით შეიძლება მომავალში გადაეტანათ
        DateTime end = now.AddMinutes(WorkTimeFixRules.LuftMinutes(request.LuftMinutes));
        if (end <= workHour.WhStart)
        {
            return WorkHourErrors.EndMustBeAfterStart;
        }

        workHour.WhEnd = end;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WorkHourResponse(workHour.WhId, employee.Id, employee.Name, workHour.WhStart, workHour.WhEnd);
    }
}
