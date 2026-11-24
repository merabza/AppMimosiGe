using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების ფორმის ცნობარების გამოთვლილი ნაწილი (GetGroupOrderFormLookups): ჯგუფის მდგომარეობა ბრძანების თარიღზე და
///     მოსწავლეების ნაგულისხმევი ტარიფი (D187, D218)
/// </summary>
public static class GroupOrderFormLookups
{
    /// <summary>
    ///     ჯგუფის ველები და orderDate-ზე მოქმედი სტრიქონები. startDate: საწყისი ან შექმნის ბრძანების თარიღი; null, თუ
    ///     ჯგუფს ასეთი ბრძანება არ აქვს, ამიტომ მას ბრძანება ვერ შეექმნება (D202). studentNames: „გვარი სახელი / ნომერი"
    ///     მოქმედი მოსწავლეების კონტრაქტებით
    /// </summary>
    public static GroupOrderGroupStateResponse ToGroupState(int grpId, GroupState state, DateTime orderDate,
        DateTime? startDate, IReadOnlyDictionary<int, string> studentNames)
    {
        GroupFields fields = state.Fields;
        return new GroupOrderGroupStateResponse(grpId, fields.GroupCode, fields.AcademicYearId, fields.CourseId,
            fields.GroupSizeId, fields.StudentStatusId, state.VoidDate, startDate,
            GroupActivity.IsActiveOn(state, orderDate), startDate is not null,
            GroupOrderFormulas.WeeklyHours(state.DayTimePlaces, orderDate),
            [
                .. state.Teachers.Where(t => GroupActivity.IsRowActiveOn(t.StartDate, t.EndDate, orderDate))
                    .OrderBy(t => t.StartDate).ThenBy(t => t.LineId).Select(t =>
                        new GroupTeacherResponse(t.LineId, t.TeacherContractId, t.SalarySchemaId, t.StartDate,
                            t.EndDate))
            ],
            [
                .. ActiveStudents(state, orderDate)
                    .OrderBy(s => studentNames[s.StudentContractId], StringComparer.Ordinal).ThenBy(s => s.LineId)
                    .Select(s => new GroupStudentResponse(s.LineId, s.StudentContractId,
                        studentNames[s.StudentContractId], s.FourWeekHours, s.FourWeekFee, s.OneHourFee,
                        s.HoursCoefficient, s.StartDate, s.EndDate, s.Note))
            ],
            [
                .. state.DayTimePlaces.Where(d => GroupActivity.IsRowActiveOn(d.StartDate, d.EndDate, orderDate))
                    .OrderBy(d => d.WeekDayId).ThenBy(d => d.LineId).Select(d =>
                        new GroupDayTimePlaceResponse(d.LineId, d.WeekDayId, d.LessonStartTimeId, d.HoursCount,
                            d.RoomId, d.StartDate, d.EndDate))
            ]);
    }

    /// <summary>
    ///     ჯგუფის orderDate-ზე მოქმედი მოსწავლის სტრიქონები
    /// </summary>
    public static IEnumerable<GroupStudentRow> ActiveStudents(GroupState state, DateTime orderDate)
    {
        return state.Students.Where(s => GroupActivity.IsRowActiveOn(s.StartDate, s.EndDate, orderDate));
    }

    /// <summary>
    ///     კონტრაქტების ნაგულისხმევი ტარიფი ჯგუფის საგნითა და ზომით (ბრძანებები 1, 3; D58). კონტრაქტი, რომელსაც ასეთი
    ///     დეტალი არ აქვს, სიაში არ არის
    /// </summary>
    public static List<GroupOrderStudentTariffResponse> StudentTariffs(
        IEnumerable<GroupStudentContractLookupResponse> contracts, int courseId, int groupSizeId)
    {
        List<GroupOrderStudentTariffResponse> tariffs = [];
        foreach (GroupStudentContractLookupResponse contract in contracts)
        {
            if (GroupOrderTariffDefaults.StudentTariff(ContractTariffs(contract), courseId, groupSizeId) is
                { } tariff)
            {
                tariffs.Add(ToResponse(contract.ScId, tariff));
            }
        }

        return tariffs;
    }

    /// <summary>
    ///     გადაყვანის (5) ნაგულისხმევი ტარიფი: დეტალი სამიზნე ჯგუფის საგნითა და ზომით, ხოლო „საიდან" ჯგუფის მოსწავლეს, თუ
    ///     ასეთი დეტალი არ არის, მისი სტრიქონის ტარიფი (D187). sourceRows: „საიდან" ჯგუფის ბრძანების თარიღზე მოქმედი
    ///     სტრიქონები; მოსწავლეს ჯგუფში ერთ დღეს ერთი სტრიქონი აქვს (D55)
    /// </summary>
    public static List<GroupOrderStudentTariffResponse> TransferStudentTariffs(
        IEnumerable<GroupStudentContractLookupResponse> contracts, GroupFields target,
        IEnumerable<GroupStudentRow> sourceRows)
    {
        Dictionary<int, GroupStudentRow> rows = sourceRows.ToDictionary(s => s.StudentContractId);
        List<GroupOrderStudentTariffResponse> tariffs = [];
        foreach (GroupStudentContractLookupResponse contract in contracts)
        {
            List<StudentContractTariff> contractTariffs = ContractTariffs(contract);
            GroupStudentTariff? tariff = rows.TryGetValue(contract.ScId, out GroupStudentRow? row)
                ? GroupOrderTariffDefaults.TransferStudentTariff(contractTariffs, target, row)
                : GroupOrderTariffDefaults.StudentTariff(contractTariffs, target.CourseId, target.GroupSizeId);
            if (tariff is not null)
            {
                tariffs.Add(ToResponse(contract.ScId, tariff));
            }
        }

        return tariffs;
    }

    private static List<StudentContractTariff> ContractTariffs(GroupStudentContractLookupResponse contract)
    {
        return
        [
            .. contract.Tariffs.Select(t =>
                new StudentContractTariff(t.CourseId, t.GroupSizeId, t.FourWeekHours, t.FourWeekFee, t.OneHourFee))
        ];
    }

    private static GroupOrderStudentTariffResponse ToResponse(int studentContractId, GroupStudentTariff tariff)
    {
        return new GroupOrderStudentTariffResponse(studentContractId, tariff.FourWeekHours, tariff.FourWeekFee,
            tariff.OneHourFee, tariff.HoursCoefficient);
    }
}
