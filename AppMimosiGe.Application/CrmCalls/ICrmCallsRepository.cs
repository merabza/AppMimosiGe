using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.CrmCalls;

/// <summary>
///     CRM ზარების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface ICrmCallsRepository
{
    Task<CrmCallsRowsDataResponse> GetRowsData(CrmCallsListQuery query, CancellationToken cancellationToken = default);

    Task<CrmCallResponse?> GetOne(int crmCallId, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად, EF-ის თვალყურის დევნებით
    Task<CrmCall?> GetForChange(int crmCallId, CancellationToken cancellationToken = default);

    Task<bool> StudentContractExists(int scId, CancellationToken cancellationToken = default);
    Task<bool> CallTypeExists(int cctId, CancellationToken cancellationToken = default);
    Task<bool> AnswerTypeExists(int catId, CancellationToken cancellationToken = default);

    //ზარის ტიპები და შედეგები სახელით დალაგებული, როგორც Access-ის ფორმის ჩამოსაშლელ სიებში
    Task<List<LookupItemResponse>> GetCallTypes(CancellationToken cancellationToken = default);
    Task<List<LookupItemResponse>> GetAnswerTypes(CancellationToken cancellationToken = default);

    //სასწავლო წლის კონტრაქტები "გვარი სახელი / ნომერი"-თ, ამ სახელით დალაგებული (Access-ის ფორმის ჩამოსაშლელი სია)
    Task<List<LookupItemResponse>> GetStudentContracts(int academicYearId,
        CancellationToken cancellationToken = default);

    void Add(CrmCall crmCall);
    void Remove(CrmCall crmCall);
}
