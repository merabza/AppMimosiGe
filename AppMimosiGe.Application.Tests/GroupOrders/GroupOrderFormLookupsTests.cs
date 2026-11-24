using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrderFormLookupsTests
{
    private static readonly Dictionary<int, string> Names =
        new() { [20] = "Beta Bob / 6.020", [21] = "Alpha Ann / 6.021", [22] = "Gamma Gia / 6.022" };

    private static GroupStudentContractLookupResponse Contract(int scId,
        params StudentContractDetailResponse[] tariffs)
    {
        return new GroupStudentContractLookupResponse(scId, $"კონტრაქტი {scId}", [.. tariffs]);
    }

    //დეტალი: 4 კვირაში 8 საათი, საათის ფასი oneHourFee. ჯგუფს საგანი 6 და ზომა 2 აქვს (GroupOrdersTestData.Fields)
    private static StudentContractDetailResponse Detail(int courseId, int groupSizeId, decimal oneHourFee)
    {
        return new StudentContractDetailResponse(1, courseId, groupSizeId, 8f, 8 * oneHourFee, oneHourFee);
    }

    //ველები და მხოლოდ ბრძანების თარიღზე მოქმედი სტრიქონები: მოსწავლე 21 01.10-ს დასრულდა, მასწავლებელი 6 და ხუთშაბათის
    //განრიგი 01.12-ს იწყება. სტრიქონის Id ხაზისაა, მოსწავლეები სახელით ლაგდება
    [Fact]
    public void ToGroupState_MapsTheFieldsAndTheRowsActiveOnTheOrderDate()
    {
        GroupState state = State(teachers:
            [TeacherRow(Day(9, 1), Day(12, 1)), new GroupTeacherRow(7, 6, 9, Day(12, 1), null)], students:
            [
                StudentRow(Day(9, 1)), StudentLineRow(5, 21, Day(9, 1), Day(10, 1)),
                StudentLineRow(6, 22, Day(9, 15), null, 7m, "შენიშვნა")
            ], dayTimePlaces:
            [
                DayTimePlaceRow(Day(9, 1)), new GroupDayTimePlaceRow(8, 3, 4, 1.5f, 2, Day(9, 1), null),
                new GroupDayTimePlaceRow(9, 4, 4, 1f, 2, Day(12, 1), null)
            ]);

        GroupOrderGroupStateResponse response =
            GroupOrderFormLookups.ToGroupState(4, state, Day(11, 2), Day(9, 1), Names);

        Assert.Equal(4, response.GrpId);
        Assert.Equal("601", response.GroupCode);
        Assert.Equal(11, response.AcademicYearId);
        Assert.Equal(6, response.CourseId);
        Assert.Equal(2, response.GroupSizeId);
        Assert.Equal(10, response.StudentStatusId);
        Assert.Null(response.VoidDate);
        Assert.Equal(Day(9, 1), response.StartDate);
        Assert.True(response.IsActive);
        Assert.True(response.CanHaveOrders);
        Assert.Equal(3.5f, response.WeeklyHours);
        Assert.Equal([new GroupTeacherResponse(1, 5, 8, Day(9, 1), Day(12, 1))], response.Teachers);
        Assert.Equal([
            new GroupStudentResponse(2, 20, "Beta Bob / 6.020", 8f, 48m, 6m, 1f, Day(9, 1), null, null),
            new GroupStudentResponse(6, 22, "Gamma Gia / 6.022", 8f, 56m, 7m, 1f, Day(9, 15), null, "შენიშვნა")
        ], response.Students);
        Assert.Equal([
            new GroupDayTimePlaceResponse(3, 1, 4, 2f, 1, Day(9, 1), null),
            new GroupDayTimePlaceResponse(8, 3, 4, 1.5f, 2, Day(9, 1), null)
        ], response.DayTimePlaces);
    }

    //ბრძანების თარიღზე დახურული ჯგუფი უმოქმედოა (D200); საწყისი ან შექმნის ბრძანების გარეშე მას ბრძანება ვერ
    //შეექმნება (D202)
    [Fact]
    public void ToGroupState_ClosedGroupWithoutCreatingOrder_IsInactiveAndCannotHaveOrders()
    {
        GroupOrderGroupStateResponse response =
            GroupOrderFormLookups.ToGroupState(4, State(Day(11, 2)), Day(11, 2), null, Names);

        Assert.False(response.IsActive);
        Assert.False(response.CanHaveOrders);
        Assert.Null(response.StartDate);
        Assert.Equal(Day(11, 2), response.VoidDate);
    }

    //D58: ჯგუფის საგნისა და ზომის დეტალი კოეფიციენტით 1; კონტრაქტი ასეთი დეტალის გარეშე სიაში არ არის
    [Fact]
    public void StudentTariffs_TakeTheDetailOfTheCourseAndSize()
    {
        List<GroupOrderStudentTariffResponse> tariffs = GroupOrderFormLookups.StudentTariffs(
        [
            Contract(20, Detail(5, 2, 9m), Detail(6, 2, 6m)), Contract(21, Detail(6, 3, 7m)),
            Contract(22, Detail(6, 2, 7m))
        ], 6, 2);

        Assert.Equal([
            new GroupOrderStudentTariffResponse(20, 8f, 48m, 6m, 1f),
            new GroupOrderStudentTariffResponse(22, 8f, 56m, 7m, 1f)
        ], tariffs);
    }

    //D187: სამიზნე ჯგუფის საგნისა და ზომის დეტალი, ხოლო „საიდან" ჯგუფის მოსწავლეს, თუ ასეთი დეტალი არ არის, მისი
    //სტრიქონის ტარიფი. სხვა მოსწავლე დეტალის გარეშე სიაში არ არის
    [Fact]
    public void TransferStudentTariffs_FallBackToTheSourceRowOfTheSourceStudents()
    {
        GroupFields target = Fields with { CourseId = 6, GroupSizeId = 3 };
        GroupStudentRow sourceRow = StudentLineRow(5, 21, Day(9, 1), null, 7m) with { HoursCoefficient = 1.5f };

        List<GroupOrderStudentTariffResponse> tariffs = GroupOrderFormLookups.TransferStudentTariffs(
        [
            Contract(20, Detail(6, 3, 9m)), Contract(21, Detail(6, 2, 6m)), Contract(22, Detail(6, 2, 6m)),
            Contract(23, Detail(6, 3, 10m))
        ], target, [StudentRow(Day(9, 1)), sourceRow]);

        Assert.Equal([
            new GroupOrderStudentTariffResponse(20, 8f, 72m, 9m, 1f),
            new GroupOrderStudentTariffResponse(21, 8f, 56m, 7m, 1.5f),
            new GroupOrderStudentTariffResponse(23, 8f, 80m, 10m, 1f)
        ], tariffs);
    }

    [Fact]
    public void ActiveStudents_AreTheRowsActiveOnTheDate()
    {
        GroupState state = State(students:
            [StudentRow(Day(9, 1), Day(10, 1)), StudentLineRow(5, 21, Day(10, 1), null)]);

        Assert.Equal([21], GroupOrderFormLookups.ActiveStudents(state, Day(10, 1)).Select(s => s.StudentContractId));
    }
}
