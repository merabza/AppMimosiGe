using System;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ბრძანების თარიღი და უფლება (D184, D196, D210, D211): დღეს არის 24.11
public sealed class GroupOrderDateRuleTests
{
    private static readonly DateTime Today = Day(11, 24);

    //ბრძანება თარიღით month.day; დაპაუზებულს ჯგუფი არ აქვს
    private static GroupOrderData At(int month, int day, EGroupOrderType orderType = EGroupOrderType.AddStudents,
        bool isPaused = false)
    {
        return new GroupOrderData
        {
            Id = 1,
            OrderType = orderType,
            OrderDate = Day(month, day),
            IsPaused = isPaused,
            GroupId = isPaused ? null : 42
        };
    }

    private static GroupOrderData Paused(int month, int day)
    {
        return At(month, day, EGroupOrderType.CreateGroup, true);
    }

    private static Error? Check(GroupOrderData? before, GroupOrderData? after, bool canChangePastOrders = false)
    {
        return GroupOrderDateRule.Check(before, after, Today, canChangePastOrders);
    }

    //D184: დღევანდელი და მომავალი თარიღი უფლების გარეშეც შეიძლება
    [Theory]
    [InlineData(11, 24)]
    [InlineData(11, 25)]
    public void Check_CreateTodayOrLater_IsAllowed(int month, int day)
    {
        Assert.Null(Check(null, At(month, day)));
    }

    [Fact]
    public void Check_CreateInThePast_NeedsTheRight()
    {
        Assert.Equal(GroupOrderErrors.PastOrderNeedsRight, Check(null, At(11, 23)));
        Assert.Null(Check(null, At(11, 23), true));
    }

    //შეცვლისას ძველი თარიღიც და ახალიც მოწმდება: წარსული ბრძანება არ იცვლება და წარსულში არ გადადის
    [Theory]
    [InlineData(11, 24, 11, 24, false)]
    [InlineData(11, 25, 11, 30, false)]
    [InlineData(11, 23, 11, 25, true)]
    [InlineData(11, 25, 11, 23, true)]
    public void Check_Update_ChecksTheOldAndTheNewDate(int oldMonth, int oldDay, int newMonth, int newDay,
        bool needsTheRight)
    {
        GroupOrderData before = At(oldMonth, oldDay);
        GroupOrderData after = At(newMonth, newDay);

        Assert.Equal(needsTheRight ? GroupOrderErrors.PastOrderNeedsRight : null, Check(before, after));
        Assert.Null(Check(before, after, true));
    }

    //D191: წარსული ბრძანების (შექმნისაც) წაშლა მხოლოდ უფლებით
    [Theory]
    [InlineData(EGroupOrderType.AddStudents)]
    [InlineData(EGroupOrderType.CreateGroup)]
    public void Check_DeletePastOrder_NeedsTheRight(EGroupOrderType orderType)
    {
        Assert.Null(Check(At(11, 24, orderType), null));
        Assert.Equal(GroupOrderErrors.PastOrderNeedsRight, Check(At(11, 23, orderType), null));
        Assert.Null(Check(At(11, 23, orderType), null, true));
    }

    //დღე დროის გარეშე ედრება: დღევანდელი ბრძანება დღის შუაშიც დღევანდელია
    [Fact]
    public void Check_TodayWithATime_IsStillToday()
    {
        Assert.Null(GroupOrderDateRule.Check(null, At(11, 24), Today.AddHours(15.5), false));
    }

    //D184: დაპაუზებული ბრძანება ძველი თარიღითაც იცვლება, მაგრამ ინახება მხოლოდ a ≥ დღეს
    [Fact]
    public void Check_PausedOrderWithAnOldDate_IsSavedWithANewDate()
    {
        Assert.Null(Check(Paused(11, 1), Paused(11, 24)));
        Assert.Equal(GroupOrderErrors.PausedOrderDateIsInPast, Check(Paused(11, 1), Paused(11, 23)));
    }

    [Fact]
    public void Check_NewPausedOrderInThePast_IsNotSaved()
    {
        Assert.Equal(GroupOrderErrors.PausedOrderDateIsInPast, Check(null, Paused(11, 23)));
    }

    //D186: გააქტიურებისას (მასწავლებელი და განრიგი შეივსო) ახალი თარიღიც a ≥ დღეს
    [Fact]
    public void Check_ActivatingAPausedOrder_NeedsTodayOrLater()
    {
        Assert.Null(Check(Paused(11, 1), At(11, 24, EGroupOrderType.CreateGroup)));
        Assert.Equal(GroupOrderErrors.PausedOrderDateIsInPast,
            Check(Paused(11, 1), At(11, 23, EGroupOrderType.CreateGroup)));
    }

    //D210: უფლება დაპაუზებულ ბრძანებასაც აძლევს წარსულ თარიღს: შენახვისას, შექმნისას და გააქტიურებისას
    [Fact]
    public void Check_PausedOrderInThePast_IsAllowedWithTheRight()
    {
        Assert.Null(Check(Paused(11, 1), Paused(11, 23), true));
        Assert.Null(Check(null, Paused(11, 23), true));
        Assert.Null(Check(Paused(11, 1), At(11, 23, EGroupOrderType.CreateGroup), true));
    }

    //D211: დაპაუზებული ბრძანება ჯგუფს არ ცვლის, ამიტომ წარსული თარიღითაც იშლება უფლების გარეშე
    [Fact]
    public void Check_DeletePausedOrderInThePast_IsAllowed()
    {
        Assert.Null(Check(Paused(11, 1), null));
    }

    //D196: საწყისი ბრძანება ხელით არ იქმნება, უფლებითაც; სხვა ტიპის ბრძანება საწყისად ვერ გადაკეთდება
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Check_CreateInitialOrder_IsRefused(bool canChangePastOrders)
    {
        Assert.Equal(GroupOrderErrors.InitialOrderIsCreatedByTransitionOnly,
            Check(null, At(11, 25, EGroupOrderType.Initial), canChangePastOrders));
        Assert.Equal(GroupOrderErrors.InitialOrderIsCreatedByTransitionOnly,
            Check(At(11, 25), At(11, 25, EGroupOrderType.Initial), canChangePastOrders));
    }

    //D196: საწყისს მხოლოდ უფლებით ცვლიან, თარიღის მიუხედავად: დღევანდელსაც უფლება სჭირდება, წარსულს კი მეტი არაფერი
    [Theory]
    [InlineData(9, 1)]
    [InlineData(11, 25)]
    public void Check_ChangeInitialOrder_NeedsTheRightWhateverTheDate(int month, int day)
    {
        GroupOrderData initial = At(month, day, EGroupOrderType.Initial);

        Assert.Equal(GroupOrderErrors.InitialOrderNeedsRight, Check(initial, initial));
        Assert.Null(Check(initial, initial, true));
    }

    //D196: საწყისი ბრძანება არ იშლება, უფლებითაც
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Check_DeleteInitialOrder_IsRefused(bool canChangePastOrders)
    {
        Assert.Equal(GroupOrderErrors.InitialOrderCannotBeDeleted,
            Check(At(11, 25, EGroupOrderType.Initial), null, canChangePastOrders));
    }
}
