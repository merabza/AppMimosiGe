using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Balances.Models;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.AcademicYears.Models;

/// <summary>
///     ჯგუფის დახურვა გენერაციამდე: ძველი VoidDate, დაუსრულებელი სტრიქონები (დასრულება ცარიელია ან დახურვის თარიღზე
///     გვიანაა ან ტოლია) და დახურვის თარიღის შემდგომი თითო გაკვეთილის დარიცხვა (ბალანსის იგივე ფორმულით:
///     BalanceOperations.ChargeAmount; გაუქმებული გაკვეთილი არ ირიცხება). ჯგუფს VoidDate = closeDate და DirtyLessons ესმება
/// </summary>
public sealed class GroupClosing
{
    //გაკვეთილის სტატუსი "გაუქმდა" (როგორც ბალანსებში)
    private const int CancelledLessonStatusId = 2;

    private GroupClosing(DateTime? previousVoidDate, int openTeacherRowsCount, int openStudentRowsCount,
        int openScheduleRowsCount, IReadOnlyDictionary<int, decimal> lessonCharges)
    {
        PreviousVoidDate = previousVoidDate;
        OpenTeacherRowsCount = openTeacherRowsCount;
        OpenStudentRowsCount = openStudentRowsCount;
        OpenScheduleRowsCount = openScheduleRowsCount;
        LessonCharges = lessonCharges;
    }

    public DateTime? PreviousVoidDate { get; }
    public int OpenTeacherRowsCount { get; }
    public int OpenStudentRowsCount { get; }
    public int OpenScheduleRowsCount { get; }

    //გაკვეთილის ID → დარიცხვების ჯამი (დახურვის თარიღიდან)
    public IReadOnlyDictionary<int, decimal> LessonCharges { get; }

    public static GroupClosing Apply(Group group, DateTime closeDate)
    {
        Dictionary<int, GroupByStudent> groupStudents = group.GroupsByStudents.ToDictionary(g => g.GbsId);
        Dictionary<int, decimal> lessonCharges = group.Lessons
            .Where(l => l.LessonDt >= closeDate && l.LessonStatusId != CancelledLessonStatusId).ToDictionary(l => l.Id,
                l => l.LessonsByStudents.Sum(s =>
                    s.GroupByStudentId is { } gbsId && groupStudents.TryGetValue(gbsId, out GroupByStudent? row) &&
                    row.StudentContractId == s.StudentContractId
                        ? BalanceOperations.ChargeAmount(row.FourWeekFee, row.FourWeekHours, s.HoursCount)
                        : 0m));

        GroupClosing closing = new(group.VoidDate, group.GroupsByTeachers.Count(r => IsOpen(r.EndDate, closeDate)),
            group.GroupsByStudents.Count(r => IsOpen(r.EndDate, closeDate)),
            group.GroupDayTimePlaces.Count(r => IsOpen(r.EndDate, closeDate)), lessonCharges);

        group.VoidDate = closeDate;
        group.DirtyLessons = true;
        return closing;
    }

    //წაშლილი გაკვეთილების დარიცხვების ჯამი
    public decimal DeletedLessonsCharges(IEnumerable<int> deletedLessonIds)
    {
        return BalanceOperations.RoundMoney(deletedLessonIds.Sum(id => LessonCharges.GetValueOrDefault(id)));
    }

    private static bool IsOpen(DateTime? endDate, DateTime closeDate)
    {
        return endDate is null || endDate >= closeDate;
    }
}
