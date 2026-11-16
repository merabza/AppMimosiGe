using AppMimosiGe.Application.Lessons.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class LessonVoidStatusRuleTests
{
    [Fact]
    public void NotCancelledStatusId_IsOne()
    {
        // Assert
        Assert.Equal(1, LessonVoidStatusRule.NotCancelledStatusId);
    }

    // only "not cancelled" (1) allows attendance and a recover date; 2 and 3 are cancellations
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void AllowsPresentStudentsAndRecoverDate_OnlyForNotCancelled(int lessonStatusId, bool expected)
    {
        // Act
        bool allowed = LessonVoidStatusRule.AllowsPresentStudentsAndRecoverDate(lessonStatusId);

        // Assert
        Assert.Equal(expected, allowed);
    }

    [Theory]
    [InlineData(1, true, true, false)]
    [InlineData(1, false, false, false)]
    [InlineData(2, false, false, false)]
    [InlineData(3, false, false, false)]
    [InlineData(2, true, false, true)]
    [InlineData(2, false, true, true)]
    [InlineData(3, true, false, true)]
    [InlineData(3, false, true, true)]
    [InlineData(3, true, true, true)]
    public void IsBroken_CancelledWithAttendanceOrRecoverDate(int lessonStatusId, bool hasPresentStudent,
        bool hasRecoverDate, bool expected)
    {
        // Act
        bool broken = LessonVoidStatusRule.IsBroken(lessonStatusId, hasPresentStudent, hasRecoverDate);

        // Assert
        Assert.Equal(expected, broken);
    }
}
