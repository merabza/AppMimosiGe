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

namespace AppMimosiGe.Application.WorkHours.StartWork;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StartWorkCommandHandler(
    IWorkHoursRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<StartWorkCommand, WorkHourResponse>
{
    //Access-ის cmdFixWorkStart: თუ თანამშრომელს დღეს დაწყებული ჩანაწერი უკვე აქვს, შეცდომაა, სხვა შემთხვევაში
    //იქმნება ჩანაწერი WhStart = ახლა - ლუფტი. Access-ისგან განსხვავებით კონტრაქტი დღეს უნდა მოქმედებდეს
    public async Task<Result<WorkHourResponse>> Handle(StartWorkCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ს, თანამშრომლის არჩევას და ლუფტს უკვე ამოწმებს
        WorkTimeFixRequest request = command.Request!;
        WorkHourEmployee? employee = await repository.GetEmployee(request.TeacherContractId!.Value, cancellationToken);
        if (employee is null)
        {
            return WorkHourErrors.EmployeeNotFound;
        }

        DateTime now = WorkTimeFixRules.Now(timeProvider);
        if (!employee.IsActiveOn(now))
        {
            return WorkHourErrors.ContractIsNotActive;
        }

        if (await repository.HasRecordOnDay(employee.Id, now.Date, cancellationToken))
        {
            return WorkHourErrors.TodayRecordExists;
        }

        var workHour = new WorkHour
        {
            TeacherContractId = employee.Id,
            WhStart = now.AddMinutes(-WorkTimeFixRules.LuftMinutes(request.LuftMinutes))
        };
        repository.Add(workHour);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WorkHourResponse(workHour.WhId, employee.Id, employee.Name, workHour.WhStart, workHour.WhEnd);
    }
}
