using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.StudentContracts.Validation;

public static class StudentContractMapper
{
    //NextPayDate და DirtyNextPayDate აქ არ იცვლება: მათ დარიცხვების ნაწილი (12) ითვლის
    public static void ApplyFields(StudentContract studentContract, StudentContractRequest request)
    {
        studentContract.ContractNumber = request.ContractNumber!;
        studentContract.ContractDate = request.ContractDate;
        studentContract.StudentHumanId = request.StudentHumanId;
        studentContract.PayerHumanId = request.PayerHumanId;
        studentContract.AcademicYearId = request.AcademicYearId;
        studentContract.StudentStatusId = request.StudentStatusId;
        studentContract.DesiredMonthlyPaymentDay = request.DesiredMonthlyPaymentDay;
    }

    public static StudentContractDetail CreateDetail(StudentContractDetailRequest request)
    {
        var detail = new StudentContractDetail();
        ApplyDetailFields(detail, request);
        return detail;
    }

    /// <summary>
    ///     დეტალების სინქრონიზაცია: Id 0 ემატება, არსებული იცვლება, მოთხოვნაში არარსებული იშლება.
    ///     აბრუნებს false-ს (და არაფერს ცვლის), თუ მოთხოვნაში ისეთი Id-ია, რომელიც ამ კონტრაქტს არ ეკუთვნის
    /// </summary>
    public static bool SyncDetails(StudentContract studentContract,
        IReadOnlyCollection<StudentContractDetailRequest> requests)
    {
        Dictionary<int, StudentContractDetail> existing =
            studentContract.StudentContractDetails.ToDictionary(detail => detail.Id);
        if (requests.Any(request => request.Id != 0 && !existing.ContainsKey(request.Id)))
        {
            return false;
        }

        HashSet<int> requestedIds = [.. requests.Where(request => request.Id != 0).Select(request => request.Id)];
        foreach (StudentContractDetail detail in existing.Values.Where(detail => !requestedIds.Contains(detail.Id)))
        {
            studentContract.StudentContractDetails.Remove(detail);
        }

        foreach (StudentContractDetailRequest request in requests)
        {
            if (request.Id == 0)
            {
                studentContract.StudentContractDetails.Add(CreateDetail(request));
            }
            else
            {
                ApplyDetailFields(existing[request.Id], request);
            }
        }

        return true;
    }

    private static void ApplyDetailFields(StudentContractDetail detail, StudentContractDetailRequest request)
    {
        detail.CourseId = request.CourseId;
        detail.GroupSizeId = request.GroupSizeId;
        detail.FourWeekHours = request.FourWeekHours;
        detail.FourWeekFee = request.FourWeekFee;
        detail.OneHourFee = request.OneHourFee;
    }
}
