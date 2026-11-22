using System.Linq;
using System.Reflection;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrderErrorsTests
{
    private const string HoursMismatchStart =
        "მოსწავლის 4 კვირის საათები განრიგს არ შეესაბამება (4 × კოეფიციენტი × განრიგის კვირის საათები): ";

    private const string FeeMismatchStart =
        "მოსწავლის 4 კვირის გადასახადი საათის ფასისა და 4 კვირის საათების ნამრავლს არ უდრის: ";

    [Fact]
    public void ErrorProperties_CodeIsThePropertyName()
    {
        Assert.All(typeof(GroupOrderErrors).GetProperties(BindingFlags.Public | BindingFlags.Static),
            p => Assert.Equal(p.Name, ((Error)p.GetValue(null)!).Code));
    }

    //D185: ტექსტი იმ ბრძანების ნომერს ასახელებს, რომელიც აღარ ესადაგება
    [Fact]
    public void OrderNoLongerFits_NamesTheOrderNumber()
    {
        Error error = GroupOrderErrors.OrderNoLongerFits("26.15");

        Assert.Equal(nameof(GroupOrderErrors.OrderNoLongerFits), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(
            "ცვლილება შეუძლებელია: მის შემდეგ ბრძანება № 26.15 ჯგუფის მდგომარეობას აღარ ესადაგება " +
            "(მაგალითად, მის თარიღზე მოსწავლე ჯგუფში აღარ იქნებოდა). ჯერ ის ბრძანება შეცვალეთ ან წაშალეთ",
            error.Description);
    }

    //D187, D194: სტრიქონები მოცემული რიგით, წერტილმძიმით
    [Fact]
    public void StudentHoursDoNotMatchSchedule_ListsTheRows()
    {
        Error error = GroupOrderErrors.StudentHoursDoNotMatchSchedule(["first", "second"]);

        Assert.Equal(nameof(GroupOrderErrors.StudentHoursDoNotMatchSchedule), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(HoursMismatchStart + "first; second", error.Description);
    }

    [Fact]
    public void StudentFeeDoesNotMatchHours_TenRows_ListsThemAllWithoutACount()
    {
        Error error = GroupOrderErrors.StudentFeeDoesNotMatchHours([.. Enumerable.Range(1, 10).Select(i => $"r{i}")]);

        Assert.Equal(nameof(GroupOrderErrors.StudentFeeDoesNotMatchHours), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(FeeMismatchStart + string.Join("; ", Enumerable.Range(1, 10).Select(i => $"r{i}")),
            error.Description);
    }

    [Fact]
    public void StudentFeeDoesNotMatchHours_MoreThanTenRows_ListsTheFirstTenAndTheCount()
    {
        Error error = GroupOrderErrors.StudentFeeDoesNotMatchHours([.. Enumerable.Range(1, 12).Select(i => $"r{i}")]);

        Assert.Equal(
            FeeMismatchStart + string.Join("; ", Enumerable.Range(1, 10).Select(i => $"r{i}")) + " და სხვა, სულ 12",
            error.Description);
    }
}
