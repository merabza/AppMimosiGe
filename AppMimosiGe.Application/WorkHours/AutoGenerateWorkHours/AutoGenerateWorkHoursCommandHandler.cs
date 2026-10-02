using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class AutoGenerateWorkHoursCommandHandler(
    IWorkHoursRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse>
{
    //Access-ის cmdAutoGenerate: ყველა თანამშრომელზე (სიის თანამშრომლის ფილტრის მიუხედავად), ფილტრის პერიოდის
    //დღეებზე დღევანდელამდე; ყველა ჩანაწერი ერთი SaveChanges-ით (ერთ ტრანზაქციაში)
    public async Task<Result<WorkHoursAutoGenerateResponse>> Handle(AutoGenerateWorkHoursCommand command,
        CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ს და ორივე თარიღს უკვე ამოწმებს
        WorkHoursAutoGenerateRequest request = command.Request!;
        DateTime dateFrom = request.DateFrom!.Value.Date;
        DateTime dateTo = request.DateTo!.Value.Date;
        DateTime periodEnd = dateTo.AddDays(1);
        DateTime today = timeProvider.GetLocalNow().Date;

        List<WorkHourEmployee> employees = await repository.GetEmployees(cancellationToken);
        //დღევანდელ და მომდევნო დღეებს გენერატორი თვითონ გამოტოვებს
        List<LessonTimeData> lessonTimes = await repository.GetLessonTimes(dateFrom, periodEnd, cancellationToken);
        List<WorkHourDay> existingDays = await repository.GetRecordDays(dateFrom, periodEnd, cancellationToken);

        List<GeneratedWorkHour> generated =
            WorkHoursAutoGenerator.Plan(employees, lessonTimes, existingDays, dateFrom, dateTo, today);
        foreach (GeneratedWorkHour workHour in generated)
        {
            repository.Add(new WorkHour
            {
                TeacherContractId = workHour.TeacherContractId, WhStart = workHour.WhStart, WhEnd = workHour.WhEnd
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WorkHoursAutoGenerateResponse(generated.Count);
    }
}
