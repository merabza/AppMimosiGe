using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.SearchHumans;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SearchHumansQueryHandler(IStudentContractsRepository repository)
    : IQueryHandler<SearchHumansQuery, List<LookupItemResponse>>
{
    //მოსწავლის და გადამხდელის ძებნადი არჩევისთვის: ერთ ჯერზე მხოლოდ რამდენიმე ადამიანი იგზავნება
    public const int MaxCount = 20;
    public const int MinSearchLength = 2;

    public async Task<Result<List<LookupItemResponse>>> Handle(SearchHumansQuery request,
        CancellationToken cancellationToken)
    {
        string search = request.Search?.Trim() ?? string.Empty;
        if (search.Length < MinSearchLength)
        {
            return new List<LookupItemResponse>();
        }

        return await repository.SearchHumans(search, MaxCount, cancellationToken);
    }
}
