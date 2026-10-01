using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გენერატორის შემავალი მონაცემები ერთი ჯგუფისთვის. მასწავლებლები და განრიგი ID-ის რიგით მოდის: ერთ დღეს ორი
///     მოქმედი სტრიქონიდან პირველი გამოიყენება (Access-ის რიგი)
/// </summary>
public sealed record GroupLessonsInput(
    DateTime? VoidDate,
    IReadOnlyList<GeneratorTeacherRow> Teachers,
    IReadOnlyList<GeneratorStudentRow> Students,
    IReadOnlyList<GeneratorDayTimePlaceRow> DayTimePlaces,
    IReadOnlyList<ExistingLesson> Lessons);
