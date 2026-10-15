using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გაკვეთილის ველები, რომლებსაც გენერატორი ადგენს და არსებულ გაკვეთილს ადარებს (Access-ის
///     LessonData.isOnlyLessonEqualTo)
/// </summary>
public sealed record LessonValues(
    DateTime LessonDt,
    int TeacherContractId,
    int SalarySchemaId,
    float FourWeekHours,
    DateTime TeoMinDate,
    DateTime TeoMaxDate)
{
    //isOnlyLessonEqualTo: საათები ზღვრით შედარდება (GeneratorHours), დანარჩენი ველები ზუსტად
    public bool IsSameAs(LessonValues other)
    {
        return LessonDt == other.LessonDt && TeacherContractId == other.TeacherContractId &&
               SalarySchemaId == other.SalarySchemaId &&
               !GeneratorHours.AreDifferent(FourWeekHours, other.FourWeekHours) && TeoMinDate == other.TeoMinDate &&
               TeoMaxDate == other.TeoMaxDate;
    }
}
