using System;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     თანამშრომელი: სამუშაო საათების ჯგუფის მქონე კონტრაქტი. Name "გვარი სახელი / ნომერი"-ა. WorkHoursStart და
///     WorkHoursEnd სამუშაოს ფიქსირებული დაწყება და დასრულებაა (მნიშვნელოვანია მხოლოდ დრო, ბაზაში 1899-12-30 hh:mm)
/// </summary>
public sealed record WorkHourEmployee(
    int Id,
    string Name,
    DateTime ContractDate,
    DateTime? ContractEndDate,
    DateTime? WorkHoursStart,
    DateTime? WorkHoursEnd)
{
    //კონტრაქტი ამ დღეს მოქმედებს: დაწყებულია და (თუ დასრულების თარიღი აქვს) ჯერ არ დასრულებულა; დასრულების დღე
    //მოქმედის ჩათვლით (როგორც თანამშრომლების კონტრაქტების სიის "აქტიური")
    public bool IsActiveOn(DateTime day)
    {
        return ContractDate.Date <= day.Date && (ContractEndDate is null || day.Date <= ContractEndDate.Value.Date);
    }
}
