using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetTeacherContractFormLookupsQueryHandler(ITeacherContractsRepository repository)
    : IQueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse>
{
    public async Task<Result<TeacherContractFormLookupsResponse>> Handle(GetTeacherContractFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        return new TeacherContractFormLookupsResponse(await repository.GetRsQuoteTypes(cancellationToken),
            await repository.GetRsCountries(cancellationToken), await repository.GetSalarySchemes(cancellationToken),
            await repository.GetWorkHourGroups(cancellationToken));
    }
}
