using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.Models;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Balances;

/// <summary>
///     დარიცხვების, გადახდებისა და ბალანსების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია.
///     scIds: მხოლოდ ამ კონტრაქტების მონაცემები
/// </summary>
public interface IBalancesRepository
{
    //Access-ის vFrmChargesAndPayments-ის დარიცხვები (გაუქმებული გაკვეთილის გარდა); scIds null: ყველა კონტრაქტის
    Task<List<ChargeData>> GetCharges(IReadOnlyCollection<int>? scIds, CancellationToken cancellationToken = default);

    //scIds null: ყველა კონტრაქტის
    Task<List<PaymentData>> GetPayments(IReadOnlyCollection<int>? scIds, CancellationToken cancellationToken = default);

    //კონტრაქტის ID → "გვარი სახელი / ნომერი" (Access-ის ამონაწერის სტრიქონის ჩამოსაშლელი სია)
    Task<Dictionary<int, string>> GetStudentContractNames(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    //academicYearId null: ყველა წლის კონტრაქტები
    Task<List<DepositContractData>> GetDepositContracts(int? academicYearId,
        CancellationToken cancellationToken = default);

    //მოსწავლის ანგარიში (ნაწილი 20): ამ კონტრაქტების მოსწავლეების ყველა კონტრაქტი (ყველა წლის), კონტრაქტის ID → მოსწავლის ID
    Task<Dictionary<int, int>> GetStudentAccountContracts(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    //Access-ის vNextLessonDate: კონტრაქტის პირველი გაუქმებელი გაკვეთილი today-დან (ჩათვლით)
    Task<Dictionary<int, DateTime>> GetNextLessonDates(IReadOnlyCollection<int> scIds, DateTime today,
        CancellationToken cancellationToken = default);

    //CRM ზარები, რომლებშიც "უნდა გადაიხადოს თარიღამდე" შევსებულია
    Task<List<CrmMustPayDateData>> GetCrmMustPayDates(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    Task<List<DepositGroupStudentData>> GetGroupStudents(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    //ბოლო სამუშაო თვე (OperationMonths); null, თუ კალენდარი ცარიელია
    Task<DateTime?> GetLastOperationMonth(CancellationToken cancellationToken = default);

    //შემდეგი გადახდის თარიღის გადასათვლელად, EF-ის თვალყურის დევნებით; onlyDirty: მხოლოდ იმ მოსწავლეების ყველა
    //კონტრაქტი, რომელთაც DirtyNextPayDate-იანი კონტრაქტი აქვთ
    Task<List<StudentContract>> GetStudentContractsForRecount(bool onlyDirty,
        CancellationToken cancellationToken = default);
}
