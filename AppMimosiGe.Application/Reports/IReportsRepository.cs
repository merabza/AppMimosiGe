using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Comments.Models;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Application.Reports.WorkTime.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports;

/// <summary>
///     რეპორტების მონაცემების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IReportsRepository
{
    //განრიგი თარიღისთვის: აქტიური ჯგუფები (Access-ის vActiveGroupsForReports) და მათი მოსწავლის, მასწავლებლისა და
    //განრიგის სტრიქონები, რომლებიც ამ დღეს მოქმედებს
    Task<ScheduleSnapshot> GetSchedule(DateTime date, CancellationToken cancellationToken = default);

    //ფილტრების ჩამოსაშლელი სიები: მასწავლებლები, საგნები, მოსწავლეები
    Task<ReportLookupsResponse> GetLookups(CancellationToken cancellationToken = default);

    //გაკვეთილები, რომელთა დრო [from, to) შუალედშია (r11, r12, r34)
    Task<List<PeriodLessonRow>> GetPeriodLessons(DateTime from, DateTime to,
        CancellationToken cancellationToken = default);

    //გენერატორის ლოგის ჩანაწერები ამავე ჯგუფის გაკვეთილით (r13)
    //academicYearId: ჯგუფის სასწავლო წელი, null: ყველა წელი (ასევე GetLessonsWithMidnightTeoDates და GetGroupRows)
    Task<List<LessonErrorRow>> GetLessonErrors(int? academicYearId, CancellationToken cancellationToken = default);

    //გაკვეთილები, რომელთა TeoMinDate-ის ან TeoMaxDate-ის დრო 00:00:00-ია (r22)
    Task<List<TeoDatesLessonRow>> GetLessonsWithMidnightTeoDates(int? academicYearId,
        CancellationToken cancellationToken = default);

    //გაცდენების რაოდენობა მოსწავლის კონტრაქტით და საგნით: Present = false, სტატუსი "გაუქმდა"-ს გარდა, გაკვეთილის
    //დრო [from, to) შუალედში (r14)
    Task<List<AbsenceCountRow>> GetAbsenceCounts(DateTime from, DateTime to,
        CancellationToken cancellationToken = default);

    //r17-ის მონაცემები date დღისთვის: გაცდენები date დღეს აქტიური ჯგუფის ამ დღეს მოქმედი სტრიქონიდან და ბოლო
    //დასწრება (სტატუსი "არ გაუქმებულა"), ორივე მხოლოდ before-მდე დაწყებული გაკვეთილებიდან
    Task<MissingsInRowData> GetMissingsInRow(DateTime date, DateTime before,
        CancellationToken cancellationToken = default);

    //ჯგუფების ყველა სტრიქონი შემოწმების რეპორტებისთვის (r26–r33)
    Task<GroupRowsSnapshot> GetGroupRows(int? academicYearId, CancellationToken cancellationToken = default);

    //აქტიური ჯგუფები date დღისთვის ზომით, სტატუსით და საგნით, მათი ამ დღეს მოქმედი მოსწავლის და მასწავლებლის
    //სტრიქონები და კონტრაქტების სახელები (r08, r09, r10, r23, r24)
    Task<GroupsSnapshot> GetGroups(DateTime date, CancellationToken cancellationToken = default);

    //მოსწავლიანი გაკვეთილები, რომელთა დრო [from, to) შუალედშია, მოსწავლეებითა და კომენტარებით; teacherContractId-ით
    //მხოლოდ ამ მასწავლებლის გაკვეთილები (r01)
    Task<List<CommentLesson>> GetCommentLessons(DateTime from, DateTime to, int? teacherContractId,
        CancellationToken cancellationToken = default);

    //გადახდები "უიმედო ვალის" ანგარიშით და მათი კონტრაქტების მოსწავლეები და გადამხდელები (r15)
    Task<BlackListData> GetDesperateDebts(CancellationToken cancellationToken = default);

    //ხელფასის დეტალები, რომელთა სტრიქონის თვე [fromMonth, toMonth]-შია; teacherContractId-ით მხოლოდ ამ მასწავლებლისა
    //(r25)
    Task<List<SalaryDetailRow>> GetSalaryDetails(DateTime fromMonth, DateTime toMonth, int? teacherContractId,
        CancellationToken cancellationToken = default);

    //სამუშაო დრო [from, to): მოსწავლიანი გაკვეთილები (გაუქმებულის გარდა) ჩატარების დღით (აღდგენის თარიღი, თუ აქვს)
    //და სამუშაო საათების დასრულებული ჩანაწერები, რომლებიც ამ შუალედში იწყება და მთავრდება (r36)
    Task<WorkTimeData> GetWorkTime(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    //თვეების ქართული სახელები (GeoMonths) ნომრით (r25, r36)
    Task<IReadOnlyDictionary<int, string>> GetMonthNames(CancellationToken cancellationToken = default);
}
