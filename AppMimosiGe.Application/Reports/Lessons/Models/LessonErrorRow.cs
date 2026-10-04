using System;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     გენერატორის ლოგის ჩანაწერი (LessonsCheckCreateErrorLogs), რომელსაც ამავე ჯგუფის გაკვეთილი აქვს (r13)
/// </summary>
public sealed record LessonErrorRow(int LogId, string GroupCode, DateTime LessonDt, string ErrorText);
