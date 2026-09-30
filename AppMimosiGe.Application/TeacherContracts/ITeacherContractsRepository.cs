using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.TeacherContracts;

/// <summary>
///     თანამშრომლების კონტრაქტების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface ITeacherContractsRepository
{
    Task<TeacherContractsRowsDataResponse> GetRowsData(TeacherContractsListQuery query,
        CancellationToken cancellationToken = default);

    Task<TeacherContractResponse?> GetOne(int id, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად, EF-ის თვალყურის დევნებით
    Task<TeacherContract?> GetForChange(int id, CancellationToken cancellationToken = default);

    //ნომერი უნიკალურია ყველა კონტრაქტს შორის (Q3ა)
    Task<bool> ContractNumberExists(string contractNumber, int exceptId, CancellationToken cancellationToken = default);

    //კონტრაქტზე მიბმულია ჯგუფი, გაკვეთილი (მასწავლებლად ან შემცვლელად), ხელფასი ან სამუშაო საათები
    Task<bool> IsInUse(int id, CancellationToken cancellationToken = default);

    Task<bool> HumanExists(int humId, CancellationToken cancellationToken = default);
    Task<bool> RsQuoteTypeExists(int qtId, CancellationToken cancellationToken = default);
    Task<bool> RsCountryExists(int id, CancellationToken cancellationToken = default);
    Task<bool> SalarySchemeExists(int id, CancellationToken cancellationToken = default);
    Task<bool> WorkHourGroupExists(int whgId, CancellationToken cancellationToken = default);

    //ცნობარები სახელით დალაგებული, როგორც Access-ის ფორმის ჩამოსაშლელ სიებში
    Task<List<LookupItemResponse>> GetRsQuoteTypes(CancellationToken cancellationToken = default);
    Task<List<LookupItemResponse>> GetRsCountries(CancellationToken cancellationToken = default);
    Task<List<LookupItemResponse>> GetSalarySchemes(CancellationToken cancellationToken = default);

    //Access-ის ფორმაში ჯგუფი გასაღებით (whgKey) ჩანდა
    Task<List<LookupItemResponse>> GetWorkHourGroups(CancellationToken cancellationToken = default);

    void Add(TeacherContract teacherContract);
    void Remove(TeacherContract teacherContract);
}
