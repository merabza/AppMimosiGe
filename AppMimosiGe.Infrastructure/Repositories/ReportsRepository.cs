using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Comments.Models;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Application.Reports.WorkTime.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class ReportsRepository(IMimosiGeDbContext context) : IReportsRepository
{
    //აქტიური ჯგუფები (ActiveGroupsForReports) და მათი სტრიქონები, რომლებიც date დღეს მოქმედებს; სახელები მხოლოდ
    //ამ სტრიქონების კონტრაქტებისა
    public async Task<ScheduleSnapshot> GetSchedule(DateTime date, CancellationToken cancellationToken = default)
    {
        DateTime dayAfter = ActiveGroupsForReports.DayAfter(date);
        IQueryable<int> activeGroupIds = ActiveGroupsForReports.Query(context, date).Select(g => g.GrpId);

        List<ScheduleGroup> groups = await ActiveGroupsForReports.Query(context, date).AsNoTracking()
            .Select(g => new ScheduleGroup(g.GrpId, g.GroupCode, g.CourseId, g.Course.CourseName))
            .ToListAsync(cancellationToken);
        List<ScheduleStudentRow> students = await context.GroupsByStudents.AsNoTracking()
            .Where(s => activeGroupIds.Contains(s.GroupId) && s.StartDate < dayAfter &&
                        (s.EndDate == null || s.EndDate >= dayAfter))
            .Select(s => new ScheduleStudentRow(s.GbsId, s.GroupId, s.StudentContractId))
            .ToListAsync(cancellationToken);
        List<ScheduleTeacherRow> teachers = await context.GroupsByTeachers.AsNoTracking()
            .Where(t => activeGroupIds.Contains(t.GroupId) && t.StartDate < dayAfter &&
                        (t.EndDate == null || t.EndDate >= dayAfter))
            .Select(t => new ScheduleTeacherRow(t.Id, t.GroupId, t.TeacherContractId)).ToListAsync(cancellationToken);
        List<ScheduleLessonRow> lessons = await context.GroupDayTimePlaces.AsNoTracking()
            .Where(d => activeGroupIds.Contains(d.GroupId) && d.StartDate < dayAfter &&
                        (d.EndDate == null || d.EndDate >= dayAfter)).Select(d =>
                new ScheduleLessonRow(d.GdtpId, d.GroupId, d.WeekDayId, d.LessonStartTime.LstTime, d.HoursCount,
                    d.RoomId)).ToListAsync(cancellationToken);

        Dictionary<int, string> roomNames = await context.Rooms.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.RoomName, cancellationToken);
        //crosstab-ის სვეტების ფიქსირებული რიგი: კვირის დღის ნომრით (1 = ორშაბათი)
        List<ScheduleWeekDay> weekDays = await context.WeekDays.AsNoTracking().OrderBy(w => w.WeekDayNumber)
            .ThenBy(w => w.Id).Select(w => new ScheduleWeekDay(w.Id, w.ShortName)).ToListAsync(cancellationToken);

        List<int> teacherContractIds = [.. teachers.Select(t => t.TeacherContractId).Distinct()];
        Dictionary<int, SchedulePerson> teacherNames = await context.TeacherContracts.AsNoTracking()
            .Where(tc => teacherContractIds.Contains(tc.Id))
            .Select(tc => new KeyValuePair<int, SchedulePerson>(tc.Id,
                new SchedulePerson(tc.TeacherHuman.LastName, tc.TeacherHuman.FirstName, tc.ContractNumber)))
            .ToDictionaryAsync(p => p.Key, p => p.Value, cancellationToken);
        List<int> studentContractIds = [.. students.Select(s => s.StudentContractId).Distinct()];
        Dictionary<int, SchedulePerson> studentNames = await context.StudentContracts.AsNoTracking()
            .Where(sc => studentContractIds.Contains(sc.ScId))
            .Select(sc => new KeyValuePair<int, SchedulePerson>(sc.ScId,
                new SchedulePerson(sc.StudentHuman.LastName, sc.StudentHuman.FirstName, sc.ContractNumber)))
            .ToDictionaryAsync(p => p.Key, p => p.Value, cancellationToken);

        return new ScheduleSnapshot(groups, students, teachers, lessons, roomNames, weekDays, teacherNames,
            studentNames);
    }

    //Access-ის FrmMain-ის combo-ები: მასწავლებლები და მოსწავლეები "გვარი სახელი / ნომერი"-თ, საგნები სახელით.
    //მასწავლებლის ნომერი უნიკალურია; მოსწავლისა მხოლოდ წლის ფარგლებში, ამიტომ ერთნაირ სახელებს ID ალაგებს
    public async Task<ReportLookupsResponse> GetLookups(CancellationToken cancellationToken = default)
    {
        List<LookupItemResponse> teachers = await context.TeacherContracts.AsNoTracking()
            .Select(x => new
            {
                x.Id, Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).Select(x => new LookupItemResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
        List<LookupItemResponse> courses = await context.Courses.AsNoTracking().OrderBy(x => x.CourseName)
            .ThenBy(x => x.CrsId).Select(x => new LookupItemResponse(x.CrsId, x.CourseName))
            .ToListAsync(cancellationToken);
        List<LookupItemResponse> students = await context.StudentContracts.AsNoTracking()
            .Select(x => new
            {
                x.ScId, Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).ThenBy(x => x.ScId).Select(x => new LookupItemResponse(x.ScId, x.Name))
            .ToListAsync(cancellationToken);
        return new ReportLookupsResponse(teachers, courses, students);
    }

    //Access-ის vR11/vR12/vR34: მასწავლებელი და შემცვლელი "გვარი სახელი / ნომერი"-სთვის, დამსწრე მოსწავლე
    //(vLessonsWithPresentStudents) EXISTS-ით
    public async Task<List<PeriodLessonRow>> GetPeriodLessons(DateTime from, DateTime to,
        CancellationToken cancellationToken = default)
    {
        var lessons = await context.Lessons.AsNoTracking().Where(l => l.LessonDt >= from && l.LessonDt < to).Select(l =>
            new
            {
                l.Id,
                l.LessonDt,
                l.Group.GroupCode,
                l.TeacherContractId,
                l.SubstituteTeacherContractId,
                l.LessonStatusId,
                l.LessonStatus.StatusName,
                HasRecoverDate = l.RecoverDate != null,
                HasPresentStudent = l.LessonsByStudents.Any(s => s.Present)
            }).ToListAsync(cancellationToken);
        Dictionary<int, SchedulePerson> teachers = await TeacherPersons([
            .. lessons.Select(l => l.TeacherContractId),
            .. lessons.Select(l => l.SubstituteTeacherContractId).OfType<int>()
        ], cancellationToken);
        return
        [
            .. lessons.Select(l => new PeriodLessonRow(l.Id, l.LessonDt, l.GroupCode, l.TeacherContractId,
                teachers[l.TeacherContractId],
                l.SubstituteTeacherContractId is { } substituteId ? teachers[substituteId] : null, l.LessonStatusId,
                l.StatusName, l.HasRecoverDate, l.HasPresentStudent))
        ];
    }

    //Access-ის vR13LessonsWithErrors: ლოგი INNER JOIN Lessons (გაკვეთილი და ჯგუფი ერთმანეთს უნდა ემთხვეოდეს)
    public Task<List<LessonErrorRow>> GetLessonErrors(CancellationToken cancellationToken = default)
    {
        return context.LessonsCheckCreateErrorLogs.AsNoTracking()
            .Where(x => x.Lesson != null && x.Lesson.GroupId == x.GroupId).Select(x =>
                new LessonErrorRow(x.Id, x.Group.GroupCode, x.Lesson!.LessonDt, x.ErrorLogText.Text))
            .ToListAsync(cancellationToken);
    }

    //Access-ის vR22: Format(…, "hh:nn:ss") = "00:00:00", ანუ საათი, წუთი და წამი 0-ია (წამის ნაწილი არ ითვლება)
    public async Task<List<TeoDatesLessonRow>> GetLessonsWithMidnightTeoDates(
        CancellationToken cancellationToken = default)
    {
        var lessons = await context.Lessons.AsNoTracking().Where(l =>
            l.TeoMinDate.Hour == 0 && l.TeoMinDate.Minute == 0 && l.TeoMinDate.Second == 0 ||
            l.TeoMaxDate.Hour == 0 && l.TeoMaxDate.Minute == 0 && l.TeoMaxDate.Second == 0).Select(l => new
        {
            l.Id,
            l.Group.GroupCode,
            l.TeacherContractId,
            l.TeoMinDate,
            l.TeoMaxDate,
            l.FourWeekHours,
            l.LessonDt
        }).ToListAsync(cancellationToken);
        Dictionary<int, SchedulePerson> teachers =
            await TeacherPersons([.. lessons.Select(l => l.TeacherContractId)], cancellationToken);
        return
        [
            .. lessons.Select(l => new TeoDatesLessonRow(l.Id, l.GroupCode, teachers[l.TeacherContractId],
                l.TeoMinDate, l.TeoMaxDate, l.FourWeekHours, l.LessonDt))
        ];
    }

    //Access-ის vR14Missings: გაკვეთილის ჯგუფის საგანი; სახელი ცალკე, კონტრაქტით (Access-ის GROUP BY სახელით და
    //ნომრით ერთი წლის ბაზაში იგივეა)
    public async Task<List<AbsenceCountRow>> GetAbsenceCounts(DateTime from, DateTime to,
        CancellationToken cancellationToken = default)
    {
        var counts = await context.LessonsByStudents.AsNoTracking()
            .Where(s => !s.Present && s.Lesson.LessonStatusId != ReportLessonStatuses.Cancelled &&
                        s.Lesson.LessonDt >= from && s.Lesson.LessonDt < to)
            .GroupBy(s => new { s.StudentContractId, s.Lesson.Group.Course.CourseName })
            .Select(g => new { g.Key.StudentContractId, g.Key.CourseName, Count = g.Count() })
            .ToListAsync(cancellationToken);
        Dictionary<int, SchedulePerson> students =
            await StudentPersons([.. counts.Select(c => c.StudentContractId)], cancellationToken);
        return
        [
            .. counts.Select(c =>
                new AbsenceCountRow(c.StudentContractId, students[c.StudentContractId], c.CourseName, c.Count))
        ];
    }

    //Access-ის vR17MissingsInRow და vStudentLastPresentDate: გაცდენის სტრიქონი ებმის მოსწავლის ჯგუფის სტრიქონს
    //(GroupByStudentId და იგივე კონტრაქტი), რომელიც date დღეს მოქმედებს და აქტიური ჯგუფისაა (ActiveGroupsForReports).
    //ბოლო დასწრება ნებისმიერი ჯგუფიდანაა, მაგრამ before-მდე (D122)
    public async Task<MissingsInRowData> GetMissingsInRow(DateTime date, DateTime before,
        CancellationToken cancellationToken = default)
    {
        DateTime dayAfter = ActiveGroupsForReports.DayAfter(date);
        IQueryable<int> activeGroupIds = ActiveGroupsForReports.Query(context, date).Select(g => g.GrpId);
        List<StudentAbsence> absences = await context.LessonsByStudents.AsNoTracking()
            .Where(s => !s.Present && s.Lesson.LessonStatusId != ReportLessonStatuses.Cancelled &&
                        s.Lesson.LessonDt < before && s.GroupByStudent != null &&
                        s.GroupByStudent.StudentContractId == s.StudentContractId &&
                        s.GroupByStudent.StartDate < dayAfter &&
                        (s.GroupByStudent.EndDate == null || s.GroupByStudent.EndDate >= dayAfter) &&
                        activeGroupIds.Contains(s.GroupByStudent.GroupId))
            .Select(s => new StudentAbsence(s.StudentContractId, s.Lesson.LessonDt)).ToListAsync(cancellationToken);
        List<int> contractIds = [.. absences.Select(a => a.StudentContractId).Distinct()];
        Dictionary<int, DateTime> lastPresences = await context.LessonsByStudents.AsNoTracking()
            .Where(s => s.Present && s.Lesson.LessonStatusId == ReportLessonStatuses.NotCancelled &&
                        s.Lesson.LessonDt < before && contractIds.Contains(s.StudentContractId))
            .GroupBy(s => s.StudentContractId)
            .Select(g => new { StudentContractId = g.Key, LastPresence = g.Max(s => s.Lesson.LessonDt) })
            .ToDictionaryAsync(x => x.StudentContractId, x => x.LastPresence, cancellationToken);
        Dictionary<int, StudentContact> students = await context.StudentContracts.AsNoTracking()
            .Where(sc => contractIds.Contains(sc.ScId)).Select(sc => new
            {
                sc.ScId,
                sc.StudentHuman.LastName,
                sc.StudentHuman.FirstName,
                sc.ContractNumber,
                StudentPhone = sc.StudentHuman.PhoneNumber,
                PayerLastName = sc.PayerHuman.LastName,
                PayerFirstName = sc.PayerHuman.FirstName,
                PayerPhone = sc.PayerHuman.PhoneNumber
            }).ToDictionaryAsync(x => x.ScId,
                x => new StudentContact(new SchedulePerson(x.LastName, x.FirstName, x.ContractNumber), x.StudentPhone,
                    $"{x.PayerLastName} {x.PayerFirstName}", x.PayerPhone), cancellationToken);
        return new MissingsInRowData(absences, lastPresences, students);
    }

    //ყველა ჯგუფი და სტრიქონი, თარიღების მიუხედავად; სახელები მხოლოდ სტრიქონების კონტრაქტებისა
    public async Task<GroupRowsSnapshot> GetGroupRows(CancellationToken cancellationToken = default)
    {
        List<CheckGroup> groups = await context.Groups.AsNoTracking()
            .Select(g => new CheckGroup(g.GrpId, g.GroupCode, g.CourseId, g.Course.CourseName, g.VoidDate))
            .ToListAsync(cancellationToken);
        List<CheckStudentRow> students = await context.GroupsByStudents.AsNoTracking().Select(s =>
            new CheckStudentRow(s.GbsId, s.GroupId, s.StudentContractId, s.StartDate, s.EndDate, s.FourWeekHours,
                s.FourWeekFee, s.OneHourFee, s.HoursCoefficient)).ToListAsync(cancellationToken);
        List<CheckTeacherRow> teachers = await context.GroupsByTeachers.AsNoTracking()
            .Select(t => new CheckTeacherRow(t.Id, t.GroupId, t.TeacherContractId, t.SalarySchemaId, t.StartDate,
                t.EndDate)).ToListAsync(cancellationToken);
        List<CheckDayTimeRow> dayTimes = await context.GroupDayTimePlaces.AsNoTracking()
            .Select(d => new CheckDayTimeRow(d.GdtpId, d.GroupId, d.StartDate, d.EndDate, d.HoursCount))
            .ToListAsync(cancellationToken);

        Dictionary<int, SchedulePerson> studentNames =
            await StudentPersons([.. students.Select(s => s.StudentContractId)], cancellationToken);
        List<int> teacherContractIds = [.. teachers.Select(t => t.TeacherContractId).Distinct()];
        Dictionary<int, CheckTeacherContract> teacherContracts = await context.TeacherContracts.AsNoTracking()
            .Where(tc => teacherContractIds.Contains(tc.Id)).Select(tc => new
            {
                tc.Id,
                tc.TeacherHuman.LastName,
                tc.TeacherHuman.FirstName,
                tc.ContractNumber,
                tc.SalarySchemaByHoursId
            }).ToDictionaryAsync(x => x.Id,
                x => new CheckTeacherContract(new SchedulePerson(x.LastName, x.FirstName, x.ContractNumber),
                    x.SalarySchemaByHoursId), cancellationToken);
        Dictionary<int, string> salarySchemeNames = await context.TeacherSalarySchemes.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.SchemaName, cancellationToken);
        //Access-ის vMaxDate
        DateTime? maxFinishDate =
            await context.AcademicYears.MaxAsync(y => (DateTime?)y.FinishDate, cancellationToken);

        return new GroupRowsSnapshot(groups, students, teachers, dayTimes, studentNames, teacherContracts,
            salarySchemeNames, maxFinishDate);
    }

    //აქტიური ჯგუფები (ActiveGroupsForReports) და მათი სტრიქონები, რომლებიც date დღეს მოქმედებს (Access-ის vR08,
    //vR10, vR23, vR24-ის StartDate <= P და EndDate Is Null Or EndDate > P); სახელები მხოლოდ ამ სტრიქონების
    //კონტრაქტებისა
    public async Task<GroupsSnapshot> GetGroups(DateTime date, CancellationToken cancellationToken = default)
    {
        DateTime dayAfter = ActiveGroupsForReports.DayAfter(date);
        IQueryable<int> activeGroupIds = ActiveGroupsForReports.Query(context, date).Select(g => g.GrpId);

        List<ReportGroup> groups = await ActiveGroupsForReports.Query(context, date).AsNoTracking()
            .Select(g => new ReportGroup(g.GrpId, g.GroupCode, g.CourseId, g.Course.CourseName, g.GroupSizeId,
                g.GroupSize.GrsName, g.GroupSize.GrsSize, g.StudentStatusId, g.StudentStatus.StudentStatusName))
            .ToListAsync(cancellationToken);
        List<ReportGroupStudent> students = await context.GroupsByStudents.AsNoTracking()
            .Where(s => activeGroupIds.Contains(s.GroupId) && s.StartDate < dayAfter &&
                        (s.EndDate == null || s.EndDate >= dayAfter)).Select(s =>
                new ReportGroupStudent(s.GbsId, s.GroupId, s.StudentContractId, s.FourWeekHours, s.FourWeekFee,
                    s.HoursCoefficient, s.StartDate)).ToListAsync(cancellationToken);
        List<ReportGroupTeacher> teachers = await context.GroupsByTeachers.AsNoTracking()
            .Where(t => activeGroupIds.Contains(t.GroupId) && t.StartDate < dayAfter &&
                        (t.EndDate == null || t.EndDate >= dayAfter)).Select(t =>
                new ReportGroupTeacher(t.Id, t.GroupId, t.TeacherContractId, t.SalaryScheme.SchemaName, t.StartDate))
            .ToListAsync(cancellationToken);

        Dictionary<int, SchedulePerson> studentNames =
            await StudentPersons([.. students.Select(s => s.StudentContractId)], cancellationToken);
        Dictionary<int, SchedulePerson> teacherNames =
            await TeacherPersons([.. teachers.Select(t => t.TeacherContractId)], cancellationToken);
        return new GroupsSnapshot(groups, students, teachers, studentNames, teacherNames);
    }

    //Access-ის vR01CommentsQuery: გაკვეთილი INNER JOIN LessonsByStudents, მასწავლებლის ფილტრი გაკვეთილის
    //მასწავლებელზე (არა შემცვლელზე)
    public async Task<List<CommentLesson>> GetCommentLessons(DateTime from, DateTime to, int? teacherContractId,
        CancellationToken cancellationToken = default)
    {
        var lessons = await context.Lessons.AsNoTracking()
            .Where(l => l.LessonDt >= from && l.LessonDt < to &&
                        (teacherContractId == null || l.TeacherContractId == teacherContractId) &&
                        l.LessonsByStudents.Any()).Select(l => new
            {
                l.Id,
                l.LessonDt,
                l.Group.GroupCode,
                l.Group.Course.CourseName,
                l.TeacherContractId,
                l.SubstituteTeacherContractId,
                l.RecoverDate
            }).ToListAsync(cancellationToken);
        List<int> lessonIds = [.. lessons.Select(l => l.Id)];
        var students = await context.LessonsByStudents.AsNoTracking().Where(s => lessonIds.Contains(s.LessonId))
            .Select(s => new { s.LessonId, s.StudentContractId, s.TeacherComment, s.StudentComment })
            .ToListAsync(cancellationToken);

        Dictionary<int, SchedulePerson> teachers = await TeacherPersons([
            .. lessons.Select(l => l.TeacherContractId),
            .. lessons.Select(l => l.SubstituteTeacherContractId).OfType<int>()
        ], cancellationToken);
        Dictionary<int, SchedulePerson> studentNames =
            await StudentPersons([.. students.Select(s => s.StudentContractId)], cancellationToken);
        var lessonStudents = students.ToLookup(s => s.LessonId);
        return
        [
            .. lessons.Select(l => new CommentLesson(l.Id, l.LessonDt, l.GroupCode, l.CourseName,
                teachers[l.TeacherContractId],
                l.SubstituteTeacherContractId is { } substituteId ? teachers[substituteId] : null, l.RecoverDate, [
                    .. lessonStudents[l.Id].Select(s => new CommentStudent(s.StudentContractId,
                        studentNames[s.StudentContractId], s.TeacherComment, s.StudentComment))
                ]))
        ];
    }

    //Access-ის vR15BlackList: გადახდები ანგარიშით, რომელსაც DesperateDebt აქვს; ადამიანები კონტრაქტის მოსწავლე და
    //გადამხდელია
    public async Task<BlackListData> GetDesperateDebts(CancellationToken cancellationToken = default)
    {
        List<DebtPayment> payments = await context.Payments.AsNoTracking()
            .Where(p => p.BankAccount != null && p.BankAccount.DesperateDebt).Select(p => new DebtPayment(p.Id,
                p.StudentContract.StudentHumanId, p.StudentContract.PayerHumanId, p.Amount))
            .ToListAsync(cancellationToken);
        List<int> humanIds = [.. payments.SelectMany(p => new[] { p.StudentHumanId, p.PayerHumanId }).Distinct()];
        Dictionary<int, Debtor> humans = await context.Humans.AsNoTracking().Where(h => humanIds.Contains(h.HumId))
            .Select(h => new { h.HumId, h.LastName, h.FirstName, h.PersonalId })
            .ToDictionaryAsync(h => h.HumId, h => new Debtor(h.LastName, h.FirstName, h.PersonalId),
                cancellationToken);
        return new BlackListData(payments, humans);
    }

    //Access-ის vR25TecherSalaryByGroups: დეტალი, მისი სტრიქონის თვე და მასწავლებელი (Nz([LegalName], [FirstName]))
    public Task<List<SalaryDetailRow>> GetSalaryDetails(DateTime fromMonth, DateTime toMonth, int? teacherContractId,
        CancellationToken cancellationToken = default)
    {
        return context.SalaryLinesDetails.AsNoTracking()
            .Where(d => d.SalaryLine.SaMonthDate >= fromMonth && d.SalaryLine.SaMonthDate <= toMonth &&
                        (teacherContractId == null || d.SalaryLine.TeacherContractId == teacherContractId)).Select(d =>
                new SalaryDetailRow(d.SadId, d.SalaryLine.SaMonthDate, d.SalaryLine.TeacherContractId,
                    d.SalaryLine.TeacherContract.TeacherHuman.LastName,
                    d.SalaryLine.TeacherContract.TeacherHuman.LegalName ??
                    d.SalaryLine.TeacherContract.TeacherHuman.FirstName, d.SalaryLine.TeacherContract.ContractNumber,
                    d.GroupId, d.Group.GroupCode, d.Group.Course.CourseName, d.SadHoursCount, d.SadHourCost,
                    d.SadAmount)).ToListAsync(cancellationToken);
    }

    //Access-ის VR36Base1 და VR36Base2: გაკვეთილი სტატუსით "გაუქმდა"-ს გარდა და მოსწავლეებით, თანამშრომელი
    //შემცვლელი ან მასწავლებელი, საათები მოსწავლეების მაქსიმუმი; ფილტრი ჩატარების დღით (აღდგენის თარიღი, თუ აქვს;
    //Access თავდაპირველი თარიღით ფილტრავდა). სამუშაო საათების ჩანაწერი: WhStart >= from და WhEnd < to (Access-ის
    //whEnd <= "თარიღამდე" 23:59:59), დაუსრულებელი არ ითვლება
    public async Task<WorkTimeData> GetWorkTime(DateTime from, DateTime to,
        CancellationToken cancellationToken = default)
    {
        List<WorkTimeLesson> lessons = await context.Lessons.AsNoTracking()
            .Where(l => l.LessonStatusId != ReportLessonStatuses.Cancelled && (l.RecoverDate ?? l.LessonDt) >= from &&
                        (l.RecoverDate ?? l.LessonDt) < to && l.LessonsByStudents.Any()).Select(l =>
                new WorkTimeLesson(l.Id, l.SubstituteTeacherContractId ?? l.TeacherContractId, l.LessonDt,
                    l.RecoverDate, l.LessonsByStudents.Max(s => s.HoursCount))).ToListAsync(cancellationToken);
        List<WorkTimeRecord> records = await context.WorkHours.AsNoTracking()
            .Where(w => w.WhEnd != null && w.WhStart >= from && w.WhEnd < to)
            .Select(w => new WorkTimeRecord(w.WhId, w.TeacherContractId, w.WhStart, w.WhEnd!.Value))
            .ToListAsync(cancellationToken);
        Dictionary<int, SchedulePerson> employees = await TeacherPersons([
            .. lessons.Select(l => l.EmployeeId), .. records.Select(r => r.EmployeeId)
        ], cancellationToken);
        return new WorkTimeData(lessons, records, employees);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetMonthNames(CancellationToken cancellationToken = default)
    {
        return await context.GeoMonths.AsNoTracking().ToDictionaryAsync(m => m.GmnId, m => m.GmnName,
            cancellationToken);
    }

    //მასწავლებლების კონტრაქტები: გვარი, სახელი, ნომერი
    private Task<Dictionary<int, SchedulePerson>> TeacherPersons(IEnumerable<int> teacherContractIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. teacherContractIds.Distinct()];
        return context.TeacherContracts.AsNoTracking().Where(tc => ids.Contains(tc.Id))
            .Select(tc => new KeyValuePair<int, SchedulePerson>(tc.Id,
                new SchedulePerson(tc.TeacherHuman.LastName, tc.TeacherHuman.FirstName, tc.ContractNumber)))
            .ToDictionaryAsync(p => p.Key, p => p.Value, cancellationToken);
    }

    //მოსწავლეების კონტრაქტები: გვარი, სახელი, ნომერი
    private Task<Dictionary<int, SchedulePerson>> StudentPersons(IEnumerable<int> studentContractIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. studentContractIds.Distinct()];
        return context.StudentContracts.AsNoTracking().Where(sc => ids.Contains(sc.ScId))
            .Select(sc => new KeyValuePair<int, SchedulePerson>(sc.ScId,
                new SchedulePerson(sc.StudentHuman.LastName, sc.StudentHuman.FirstName, sc.ContractNumber)))
            .ToDictionaryAsync(p => p.Key, p => p.Value, cancellationToken);
    }
}
