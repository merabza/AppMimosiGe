using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.LessonGenerator.GetLessonGeneratorLog;

//გენერატორის ლოგი; GrpId: მხოლოდ ერთი ჯგუფის
public sealed record GetLessonGeneratorLogQuery(int? GrpId) : IQuery<List<LessonGeneratorLogRowResponse>>;
