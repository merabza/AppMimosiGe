using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;

//Access-ის CheckAllLessons: OnlyDirty — "ყველა ჯგუფის გაკვეთილები" (DirtyLessons-იანი ჯგუფები), სხვა შემთხვევაში
//"გადაანგარიშება" (ყველა ჯგუფი). DryRun: მხოლოდ გეგმა, ბაზაში არაფერი იწერება
public sealed record GenerateGroupsLessonsCommand(bool OnlyDirty, bool DryRun) : ICommand<LessonsGenerationResponse>;
