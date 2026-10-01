using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Lessons.GetLesson;

public sealed record GetLessonQuery(int LessonId) : IQuery<LessonResponse>;
