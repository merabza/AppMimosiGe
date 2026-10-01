using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.LessonGenerator;

/// <summary>
///     გენერატორის გეგმის კავშირი entity-ებთან: ჯგუფიდან შემავალი მონაცემები, გეგმის ჩაწერა თვალყურდევნებულ entity-ებში
///     და გეგმიდან API-ს პასუხი
/// </summary>
public static class LessonGeneratorMapper
{
    //LessonChangeResponse.Action
    public const string CreateAction = "create";
    public const string UpdateAction = "update";
    public const string DeleteAction = "delete";

    //LessonChangeResponse.ChangedFields-ის გასაღებები
    public const string LessonDtField = "lessonDt";
    public const string TeacherContractIdField = "teacherContractId";
    public const string SalarySchemaIdField = "salarySchemaId";
    public const string FourWeekHoursField = "fourWeekHours";
    public const string TeoMinDateField = "teoMinDate";
    public const string TeoMaxDateField = "teoMaxDate";

    //ჯგუფი უნდა იყოს ჩატვირთული GetGroupForGeneration-ით (განრიგი დაწყების დროით, გაკვეთილები მოსწავლეებით)
    public static GroupLessonsInput ToInput(Group group)
    {
        return new GroupLessonsInput(group.VoidDate,
        [
            .. group.GroupsByTeachers.OrderBy(t => t.Id).Select(t =>
                new GeneratorTeacherRow(t.TeacherContractId, t.SalarySchemaId, t.StartDate, t.EndDate))
        ],
        [
            .. group.GroupsByStudents.Select(s => new GeneratorStudentRow(s.GbsId, s.StudentContractId,
                s.HoursCoefficient, s.FourWeekHours, s.StartDate, s.EndDate))
        ],
        [
            .. group.GroupDayTimePlaces.OrderBy(d => d.GdtpId).Select(d => new GeneratorDayTimePlaceRow(d.WeekDayId,
                d.LessonStartTime.LstTime, d.HoursCount, d.StartDate, d.EndDate))
        ],
        [
            .. group.Lessons.Select(l => new ExistingLesson(l.Id, ToValues(l),
            [
                .. l.LessonsByStudents.Select(s => new ExistingLessonStudent(s.Id, s.StudentContractId,
                    s.GroupByStudentId, s.HoursCount, HasEnteredData(s)))
            ]))
        ]);
    }

    //Access-ის LessonStudentcontainsEnteredData: დასწრება, თემა, შეფასება ან კომენტარი. ცარიელი ტექსტიც შეტანილად
    //ითვლება (Access-ში Is Not Null)
    public static bool HasEnteredData(LessonByStudent lessonStudent)
    {
        return lessonStudent.Present || lessonStudent.Theme is not null || lessonStudent.Rate is not null ||
               lessonStudent.TeacherComment is not null || lessonStudent.StudentComment is not null;
    }

    /// <summary>
    ///     გეგმა იწერება ჯგუფის თვალყურდევნებულ entity-ებში (ჯგუფი ჩატვირთულია forChange-ით). ბაზაში ჩაწერა
    ///     SaveChanges-ით ხდება. აბრუნებს ახალ გაკვეთილებს დროის მიხედვით, რომ შენახვის შემდეგ მათი ID-ები გამოჩნდეს
    /// </summary>
    public static Dictionary<DateTime, Lesson> ApplyPlan(Group group, GroupLessonsPlan plan,
        IEnumerable<StudentContract> dirtyStudentContracts, ILessonGeneratorRepository repository, DateTime now)
    {
        Dictionary<int, Lesson> lessonsById = group.Lessons.ToDictionary(l => l.Id);
        var createdLessons = new Dictionary<DateTime, Lesson>();
        foreach (PlannedLessonChange change in plan.Changes)
        {
            switch (change.Kind)
            {
                case ELessonChangeKind.Create:
                    var lesson = new Lesson { GroupId = group.GrpId };
                    SetValues(lesson, change.Values);
                    foreach (PlannedStudentRow row in change.Students)
                    {
                        lesson.LessonsByStudents.Add(NewLessonStudent(row));
                    }

                    repository.AddLesson(lesson);
                    createdLessons[change.Values.LessonDt] = lesson;
                    break;
                case ELessonChangeKind.Update:
                    UpdateLesson(lessonsById[change.LessonId!.Value], change, repository);
                    break;
                case ELessonChangeKind.Delete:
                    repository.RemoveLesson(lessonsById[change.LessonId!.Value]);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown lesson change kind {change.Kind}");
            }
        }

        repository.ReplaceLogs(group,
            plan.Logs.Select(log => new LessonCheckCreateErrorLog
            {
                CreatedDate = now,
                GroupId = group.GrpId,
                ErrorLogTextId = log.ErrorCode,
                LessonDate = log.LessonDate,
                LessonId = log.LessonId
            }));

        if (plan.ClearDirtyLessons)
        {
            group.DirtyLessons = false;
        }

        foreach (StudentContract studentContract in dirtyStudentContracts)
        {
            studentContract.DirtyNextPayDate = true;
        }

        return createdLessons;
    }

    private static void UpdateLesson(Lesson lesson, PlannedLessonChange change, ILessonGeneratorRepository repository)
    {
        SetValues(lesson, change.Values);
        Dictionary<int, LessonByStudent> rowsById = lesson.LessonsByStudents.ToDictionary(s => s.Id);
        foreach (PlannedStudentRow row in change.Students)
        {
            switch (row.Kind)
            {
                case EStudentRowChangeKind.Add:
                    lesson.LessonsByStudents.Add(NewLessonStudent(row));
                    break;
                case EStudentRowChangeKind.Update:
                    LessonByStudent lessonStudent = rowsById[row.RowId!.Value];
                    lessonStudent.GroupByStudentId = row.GroupByStudentId;
                    lessonStudent.HoursCount = row.HoursCount;
                    break;
                case EStudentRowChangeKind.Delete:
                    repository.RemoveLessonStudent(rowsById[row.RowId!.Value]);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown student row change kind {row.Kind}");
            }
        }
    }

    private static void SetValues(Lesson lesson, LessonValues values)
    {
        lesson.LessonDt = values.LessonDt;
        lesson.TeacherContractId = values.TeacherContractId;
        lesson.SalarySchemaId = values.SalarySchemaId;
        lesson.FourWeekHours = values.FourWeekHours;
        lesson.TeoMinDate = values.TeoMinDate;
        lesson.TeoMaxDate = values.TeoMaxDate;
    }

    private static LessonByStudent NewLessonStudent(PlannedStudentRow row)
    {
        return new LessonByStudent
        {
            StudentContractId = row.StudentContractId,
            GroupByStudentId = row.GroupByStudentId,
            HoursCount = row.HoursCount
        };
    }

    private static LessonValues ToValues(Lesson lesson)
    {
        return new LessonValues(lesson.LessonDt, lesson.TeacherContractId, lesson.SalarySchemaId,
            lesson.FourWeekHours, lesson.TeoMinDate, lesson.TeoMaxDate);
    }

    /// <summary>
    ///     გეგმა API-ს პასუხად. createdLessons: შენახული ახალი გაკვეთილები (dry-run-ში ცარიელი, ID-ები მაშინ არ არის)
    /// </summary>
    public static GroupLessonsGenerationResponse ToResponse(Group group, GroupLessonsPlan plan,
        IReadOnlyDictionary<int, string> errorTexts, IReadOnlyDictionary<DateTime, Lesson> createdLessons)
    {
        List<PlannedStudentRow> updatedLessonsStudents =
            [.. plan.Changes.Where(c => c.Kind == ELessonChangeKind.Update).SelectMany(c => c.Students)];
        return new GroupLessonsGenerationResponse(group.GrpId, group.GroupCode,
            plan.Changes.Count(c => c.Kind == ELessonChangeKind.Create),
            plan.Changes.Count(c => c.Kind == ELessonChangeKind.Update && c.PreviousValues is not null),
            plan.Changes.Count(c => c.Kind == ELessonChangeKind.Delete),
            updatedLessonsStudents.Count(s => s.Kind == EStudentRowChangeKind.Add),
            updatedLessonsStudents.Count(s => s.Kind == EStudentRowChangeKind.Update),
            updatedLessonsStudents.Count(s => s.Kind == EStudentRowChangeKind.Delete),
            plan.DirtyStudentContractIds.Count,
            [
                .. plan.Logs.Select(log => new LessonGeneratorErrorResponse(log.ErrorCode,
                    errorTexts.GetValueOrDefault(log.ErrorCode, string.Empty), log.LessonDate, log.LessonId))
            ], [.. plan.Changes.Select(change => ToChangeResponse(change, createdLessons))]);
    }

    private static LessonChangeResponse ToChangeResponse(PlannedLessonChange change,
        IReadOnlyDictionary<DateTime, Lesson> createdLessons)
    {
        int? lessonId = change.Kind == ELessonChangeKind.Create
            ? createdLessons.GetValueOrDefault(change.Values.LessonDt)?.Id
            : change.LessonId;
        LessonValues? previous = change.PreviousValues;
        DateTime? previousLessonDt = previous is not null && previous.LessonDt != change.Values.LessonDt
            ? previous.LessonDt
            : null;
        bool updatedStudents = change.Kind == ELessonChangeKind.Update;
        string action = change.Kind switch
        {
            ELessonChangeKind.Create => CreateAction,
            ELessonChangeKind.Update => UpdateAction,
            ELessonChangeKind.Delete => DeleteAction,
            _ => throw new InvalidOperationException($"Unknown lesson change kind {change.Kind}")
        };
        return new LessonChangeResponse(action, lessonId, change.Values.LessonDt, previousLessonDt,
            previous is null ? [] : ChangedFields(previous, change.Values),
            updatedStudents ? change.Students.Count(s => s.Kind == EStudentRowChangeKind.Add) : 0,
            updatedStudents ? change.Students.Count(s => s.Kind == EStudentRowChangeKind.Update) : 0,
            updatedStudents ? change.Students.Count(s => s.Kind == EStudentRowChangeKind.Delete) : 0);
    }

    private static List<string> ChangedFields(LessonValues previous, LessonValues current)
    {
        List<string> fields = [];
        if (previous.LessonDt != current.LessonDt)
        {
            fields.Add(LessonDtField);
        }

        if (previous.TeacherContractId != current.TeacherContractId)
        {
            fields.Add(TeacherContractIdField);
        }

        if (previous.SalarySchemaId != current.SalarySchemaId)
        {
            fields.Add(SalarySchemaIdField);
        }

        if (!previous.FourWeekHours.Equals(current.FourWeekHours))
        {
            fields.Add(FourWeekHoursField);
        }

        if (previous.TeoMinDate != current.TeoMinDate)
        {
            fields.Add(TeoMinDateField);
        }

        if (previous.TeoMaxDate != current.TeoMaxDate)
        {
            fields.Add(TeoMaxDateField);
        }

        return fields;
    }
}
