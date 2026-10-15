using System;
using AppMimosiGe.Application.LessonGenerator.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.LessonGenerator.LessonGeneratorTestData;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class LessonValuesTests
{
    [Fact]
    public void IsSameAs_EqualValues_IsTrue()
    {
        Assert.True(SeptemberMonday(7).IsSameAs(SeptemberMonday(7)));
    }

    //D153: the hours are the same within GeneratorHours.Tolerance
    [Fact]
    public void IsSameAs_FourWeekHoursWithinTheTolerance_IsTrue()
    {
        Assert.True(SeptemberMonday(7).IsSameAs(SeptemberMonday(7) with { FourWeekHours = 8.00005f }));
        Assert.True((SeptemberMonday(7) with { FourWeekHours = 7.99995f }).IsSameAs(SeptemberMonday(7)));
    }

    [Theory]
    [InlineData(nameof(LessonValues.LessonDt))]
    [InlineData(nameof(LessonValues.TeacherContractId))]
    [InlineData(nameof(LessonValues.SalarySchemaId))]
    [InlineData(nameof(LessonValues.FourWeekHours))]
    [InlineData(nameof(LessonValues.TeoMinDate))]
    [InlineData(nameof(LessonValues.TeoMaxDate))]
    public void IsSameAs_AnyFieldChanged_IsFalse(string field)
    {
        LessonValues values = SeptemberMonday(7);
        LessonValues changed = field switch
        {
            nameof(LessonValues.LessonDt) => values with { LessonDt = Date(9, 7, 16) },
            nameof(LessonValues.TeacherContractId) => values with { TeacherContractId = 6 },
            nameof(LessonValues.SalarySchemaId) => values with { SalarySchemaId = 9 },
            nameof(LessonValues.FourWeekHours) => values with { FourWeekHours = 8.0002f },
            nameof(LessonValues.TeoMinDate) => values with { TeoMinDate = Date(9, 1) },
            nameof(LessonValues.TeoMaxDate) => values with { TeoMaxDate = Date(9, 30) },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

        Assert.False(values.IsSameAs(changed));
        Assert.False(changed.IsSameAs(values));
    }
}
