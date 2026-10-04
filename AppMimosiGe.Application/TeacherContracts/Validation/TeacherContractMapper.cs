using System;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.TeacherContracts.Validation;

public static class TeacherContractMapper
{
    //Access-ის დროის ველების თარიღი (D13): სამუშაოს დაწყება და დასრულება ინახება როგორც 1899-12-30 hh:mm
    public static readonly DateTime TimeOnlyBaseDate = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    public static void ApplyFields(TeacherContract teacherContract, TeacherContractRequest request)
    {
        teacherContract.ContractNumber = request.ContractNumber!;
        teacherContract.ContractDate = request.ContractDate.Date;
        teacherContract.TeacherHumanId = request.TeacherHumanId;
        teacherContract.BankAccount = EmptyToNull(request.BankAccount);
        teacherContract.BankAccountCode = EmptyToNull(request.BankAccountCode);
        teacherContract.PensionScheme = request.PensionScheme;
        teacherContract.IndEnt = request.IndEnt;
        teacherContract.RsQuoteTypeId = request.RsQuoteTypeId;
        teacherContract.RsCountryId = request.RsCountryId;
        teacherContract.FixedAmount = request.FixedAmount;
        teacherContract.NextMonth = request.NextMonth;
        teacherContract.Description = EmptyToNull(request.Description);
        teacherContract.SalarySchemaByHoursId = request.SalarySchemaByHoursId;
        teacherContract.WorkHourGroupId = request.WorkHourGroupId;
        teacherContract.WorkHoursStart = ToDateTime(request.WorkHoursStart);
        teacherContract.WorkHoursEnd = ToDateTime(request.WorkHoursEnd);
        teacherContract.ContractEndDate = request.ContractEndDate?.Date;
    }

    public static DateTime? ToDateTime(TimeOnly? time)
    {
        return time is null ? null : TimeOnlyBaseDate.Add(time.Value.ToTimeSpan());
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
