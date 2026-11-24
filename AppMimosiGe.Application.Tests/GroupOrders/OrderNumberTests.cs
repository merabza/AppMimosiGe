using System;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ბრძანების ნომერი (D192): „{yy}.{n}", ტექსტად ფორმატდება, რიცხვებით ლაგდება
public sealed class OrderNumberTests
{
    //yy სასწავლო წლის დაწყების წლის ბოლო ორი ციფრია
    [Theory]
    [InlineData(2026, 26)]
    [InlineData(2027, 27)]
    [InlineData(2100, 0)]
    public void YearOf_IsTheLastTwoDigitsOfTheStartYear(int startYear, int numberYear)
    {
        Assert.Equal(numberYear,
            OrderNumber.YearOf(new DateTime(startYear, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    //შემდეგი n მთვლელიდან მოდის; წლის პირველი ნომერი 1-ია
    [Theory]
    [InlineData(null, 1)]
    [InlineData(14, 15)]
    public void Next_FollowsTheCounter(int? lastNumber, int number)
    {
        Assert.Equal(new OrderNumber(26, number), OrderNumber.Next(26, lastNumber));
    }

    //n ნულების გარეშე, yy ორი ციფრით
    [Theory]
    [InlineData(26, 1, "26.1")]
    [InlineData(26, 15, "26.15")]
    [InlineData(26, 100, "26.100")]
    [InlineData(5, 3, "05.3")]
    public void ToString_FormatsYearDotNumber(int numberYear, int number, string text)
    {
        Assert.Equal(text, new OrderNumber(numberYear, number).ToString());
    }

    //26.9 < 26.10, თუმცა ტექსტით პირიქითაა; წელი ნომერზე წინაა
    [Fact]
    public void Order_IsByNumbersNotByText()
    {
        OrderNumber[] numbers = [new(26, 10), new(27, 1), new(26, 9), new(26, 100), new(25, 200)];

        Assert.Equal([new(25, 200), new(26, 9), new(26, 10), new(26, 100), new(27, 1)], numbers.Order());
        Assert.True(string.CompareOrdinal("26.10", "26.9") < 0);
    }

    [Fact]
    public void Operators_CompareByNumbers()
    {
        OrderNumber nine = new(26, 9);
        OrderNumber ten = new(26, 10);

        Assert.True(nine < ten);
        Assert.True(ten > nine);
        Assert.True(nine <= ten);
        Assert.True(ten >= nine);
        Assert.True(nine <= new OrderNumber(26, 9));
        Assert.True(nine >= new OrderNumber(26, 9));
        Assert.False(ten < nine);
        Assert.False(nine > ten);
        Assert.Equal(0, nine.CompareTo(new OrderNumber(26, 9)));
    }
}
