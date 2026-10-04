using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Finance.TeacherSalaryByGroups;

//r25TecherSalaryByGroups: მასწავლებლების გამომუშავებული ხელფასი ჯგუფების მიხედვით. მასწავლებლის კონტრაქტი
//არასავალდებულოა (null = ყველა)
public sealed record TeacherSalaryByGroupsReportQuery(DateTime StartDate, DateTime EndDate, int? TeacherContractId)
    : IQuery<ReportTable>;
