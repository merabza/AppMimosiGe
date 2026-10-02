using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.WorkHours;

/// <summary>
///     თანამშრომლების ნამუშევარი დროის რეპოზიტორია. თანამშრომელი სამუშაო საათების ჯგუფის მქონე კონტრაქტია.
///     რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IWorkHoursRepository
{
    Task<WorkHoursRowsDataResponse> GetRowsData(WorkHoursListQuery query,
        CancellationToken cancellationToken = default);

    Task<WorkHourResponse?> GetOne(int whId, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად, EF-ის თვალყურის დევნებით
    Task<WorkHour?> GetForChange(int whId, CancellationToken cancellationToken = default);

    Task<bool> EmployeeExists(int teacherContractId, CancellationToken cancellationToken = default);
    Task<WorkHourEmployee?> GetEmployee(int teacherContractId, CancellationToken cancellationToken = default);

    //ყველა თანამშრომელი, ავტომატური დაგენერირებისთვის
    Task<List<WorkHourEmployee>> GetEmployees(CancellationToken cancellationToken = default);

    //"გვარი სახელი / ნომერი"-თ, ამ სახელით დალაგებული (Access-ის ფორმის ჩამოსაშლელი სია)
    Task<List<LookupItemResponse>> GetEmployeeLookups(CancellationToken cancellationToken = default);

    //თანამშრომლის ჩანაწერი, რომელიც ამ დღეს დაიწყო (WhStart-ის თარიღით)
    Task<bool> HasRecordOnDay(int teacherContractId, DateTime day, CancellationToken cancellationToken = default);

    //ამ დღეს დაწყებული ჩანაწერებიდან ბოლოს დაწყებული, EF-ის თვალყურის დევნებით
    Task<WorkHour?> GetLastRecordOfDayForChange(int teacherContractId, DateTime day,
        CancellationToken cancellationToken = default);

    //გაუქმებული (სტატუსი 2) გაკვეთილების გარდა ყველა გაკვეთილის მოსწავლის სტრიქონი [from, toExclusive)-ში
    Task<List<LessonTimeData>> GetLessonTimes(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default);

    //ჩანაწერების დღეები (თანამშრომელი და WhStart-ის თარიღი), რომლებიც [from, toExclusive)-ში დაიწყო
    Task<List<WorkHourDay>> GetRecordDays(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default);

    void Add(WorkHour workHour);
    void Remove(WorkHour workHour);
}
