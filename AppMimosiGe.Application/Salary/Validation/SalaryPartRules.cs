using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.Validation;

/// <summary>
///     ხელით შეტანილი მდგენელის ტიპის წესები: ტიპი არსებობს, ტიპი 1 (ჩატარებული გაკვეთილების ხელფასი) მხოლოდ
///     გამოთვლით იქმნება (409), გამოქვითვა (გამოთვლის ადგილი 2) დადებითი თანხით იწერება (D104)
/// </summary>
public static class SalaryPartRules
{
    public static async Task<Error?> Check(ISalaryRepository repository, SalaryPartRequest request,
        CancellationToken cancellationToken)
    {
        //ვალიდატორი ტიპის null-ობას უკვე ამოწმებს
        int typeId = request.SalaryPartTypeId!.Value;
        if (typeId == SalaryCalculator.LessonSalaryPartTypeId)
        {
            return SalaryErrors.PartIsCalculated;
        }

        SalaryPartTypeLookupResponse? partType = await repository.GetPartType(typeId, cancellationToken);
        if (partType is null)
        {
            return SalaryErrors.PartTypeNotFound;
        }

        return partType.CountPlaceId == SalaryCalculator.DeductionCountPlaceId && request.SpAmount <= 0
            ? SalaryErrors.DeductionMustBePositive
            : null;
    }
}
