using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Payments;

/// <summary>
///     მოსწავლეების გადახდების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IPaymentsRepository
{
    Task<PaymentsRowsDataResponse> GetRowsData(PaymentsListQuery query, CancellationToken cancellationToken = default);

    Task<PaymentResponse?> GetOne(int paymentId, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად, EF-ის თვალყურის დევნებით
    Task<Payment?> GetForChange(int paymentId, CancellationToken cancellationToken = default);

    Task<bool> StudentContractExists(int scId, CancellationToken cancellationToken = default);
    Task<bool> BankAccountExists(int baId, CancellationToken cancellationToken = default);

    //გადახდის სახეები სახელით დალაგებული, როგორც Access-ის ფორმის ჩამოსაშლელ სიაში
    Task<List<LookupItemResponse>> GetBankAccounts(CancellationToken cancellationToken = default);

    //სასწავლო წლის კონტრაქტები "გვარი სახელი ნომერი"-თ, ამ სახელით დალაგებული (Access-ის ფორმის ჩამოსაშლელი სია)
    Task<List<LookupItemResponse>> GetStudentContracts(int academicYearId,
        CancellationToken cancellationToken = default);

    //dirty ალმის დასაყენებლად, EF-ის თვალყურის დევნებით
    Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    void Add(Payment payment);
    void Remove(Payment payment);
}
