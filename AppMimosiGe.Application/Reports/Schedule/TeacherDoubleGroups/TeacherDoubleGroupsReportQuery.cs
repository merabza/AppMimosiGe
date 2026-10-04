using System;

namespace AppMimosiGe.Application.Reports.Schedule.TeacherDoubleGroups;

//r21TeacherDoubleGroups: მასწავლებლები ერთ ჯგუფში რამდენჯერმე
public sealed record TeacherDoubleGroupsReportQuery(DateTime Date) : ScheduleReportQuery(Date);
