using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.WorkHours.Validation;

public static class WorkHourMapper
{
    public static void ApplyFields(WorkHour workHour, WorkHourRequest request)
    {
        workHour.TeacherContractId = request.TeacherContractId;
        workHour.WhStart = request.WhStart;
        workHour.WhEnd = request.WhEnd;
    }
}
