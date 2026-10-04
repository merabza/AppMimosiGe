using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Comments.DailyComments;

//r01Comments: დღის კომენტარები 1 თვისთვის. Date-ის თვე; მასწავლებლის კონტრაქტი არასავალდებულოა (null = ყველა)
public sealed record DailyCommentsReportQuery(DateTime Date, int? TeacherContractId) : IQuery<ReportTable>;
