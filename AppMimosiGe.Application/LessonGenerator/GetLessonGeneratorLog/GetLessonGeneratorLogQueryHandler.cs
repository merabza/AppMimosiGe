using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.LessonGenerator.GetLessonGeneratorLog;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetLessonGeneratorLogQueryHandler(ILessonGeneratorRepository repository)
    : IQueryHandler<GetLessonGeneratorLogQuery, List<LessonGeneratorLogRowResponse>>
{
    public async Task<Result<List<LessonGeneratorLogRowResponse>>> Handle(GetLessonGeneratorLogQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetLog(request.GrpId, cancellationToken);
    }
}
