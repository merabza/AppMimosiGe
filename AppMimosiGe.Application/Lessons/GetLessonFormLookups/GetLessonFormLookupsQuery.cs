using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Lessons.GetLessonFormLookups;

public sealed record GetLessonFormLookupsQuery : IQuery<LessonFormLookupsResponse>;
