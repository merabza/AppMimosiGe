using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.LessonGenerator.GenerateGroupLastLesson;

//Access-ის cmdLastLesson: ჯგუფის ბოლო გაკვეთილი, საჭიროებისას შექმნილი ან გასწორებული
public sealed record GenerateGroupLastLessonCommand(int GrpId) : ICommand<GroupLastLessonResponse>;
