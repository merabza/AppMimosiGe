using AppMimosiGe.Application.LessonGenerator.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class GeneratorHoursTests
{
    //D153: only a difference above the tolerance counts, in either order
    [Theory]
    [InlineData(1.5f, 1.5f, false)]
    [InlineData(1.5f, 1.50005f, false)]
    [InlineData(1.50005f, 1.5f, false)]
    [InlineData(GeneratorHours.Tolerance, 0f, false)]
    [InlineData(1.5f, 1.5002f, true)]
    [InlineData(1.5002f, 1.5f, true)]
    public void AreDifferent_OnlyADifferenceAboveTheToleranceCounts(float first, float second, bool expected)
    {
        Assert.Equal(expected, GeneratorHours.AreDifferent(first, second));
    }
}
