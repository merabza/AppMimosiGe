using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.LessonGenerator.GenerateGroupLessons;

//Access-ის cmdLessons: ერთი ჯგუფის გაკვეთილები. DryRun: მხოლოდ გეგმა, ბაზაში არაფერი იწერება
public sealed record GenerateGroupLessonsCommand(int GrpId, bool DryRun) : ICommand<LessonsGenerationResponse>;
