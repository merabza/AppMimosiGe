using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.GetLesson;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetLessonQueryHandler(ILessonsRepository repository) : IQueryHandler<GetLessonQuery, LessonResponse>
{
    public async Task<Result<LessonResponse>> Handle(GetLessonQuery request, CancellationToken cancellationToken)
    {
        LessonResponse? lesson = await repository.GetOne(request.LessonId, cancellationToken);
        return lesson is null ? LessonErrors.LessonNotFound : lesson;
    }
}
