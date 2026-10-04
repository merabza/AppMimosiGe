using System;

namespace AppMimosiGe.Application.Reports.Groups.GroupList;

//r10Groups: ჯგუფები. მასწავლებლის და მოსწავლის კონტრაქტი და საგანი არასავალდებულო ფილტრებია (null = ყველა)
public sealed record GroupListReportQuery(
    DateTime Date,
    int? TeacherContractId,
    int? CourseId,
    int? StudentContractId) : GroupsReportQuery(Date);
