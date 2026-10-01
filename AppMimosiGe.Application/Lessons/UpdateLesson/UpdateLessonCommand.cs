using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Lessons.UpdateLesson;

public sealed record UpdateLessonCommand(int LessonId, LessonRequest? Request) : ICommand;
