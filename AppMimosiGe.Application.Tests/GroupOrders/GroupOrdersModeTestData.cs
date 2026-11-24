using System;
using AppMimosiGe.Application.GroupOrders.Models;
using MimosiGeCore.Domain.Models;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდების ტესტების სინთეზური ბაზის ჯგუფები და ბრძანებები
internal static class GroupOrdersModeTestData
{
    //ჯგუფი ბაზიდან, სტრიქონებით: მასწავლებელი (Id grpId*10+1), ორი მოსწავლე (grpId*10+2 და +3; მეორე 15.09-ს გავიდა)
    //და ორშაბათის 2 საათი (grpId*10+4), ყველა from-იდან. 4 კვირაში 8 საათი და 48 ლარი ფორმულებს ესადაგება
    public static Group StoredGroup(int grpId, string groupCode, DateTime? from = null, DateTime? voidDate = null,
        int teacherContractId = 5)
    {
        DateTime startDate = from ?? Day(9, 1);
        var group = new Group
        {
            GrpId = grpId,
            GroupCode = groupCode,
            AcademicYearId = 11,
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            VoidDate = voidDate,
            DirtyLessons = false
        };
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = grpId * 10 + 1,
            GroupId = grpId,
            TeacherContractId = teacherContractId,
            SalarySchemaId = 8,
            StartDate = startDate
        });
        group.GroupsByStudents.Add(StudentRow(grpId * 10 + 2, grpId, 20 + grpId, startDate, null, "შენიშვნა"));
        group.GroupsByStudents.Add(StudentRow(grpId * 10 + 3, grpId, 40 + grpId, startDate, Day(9, 15), null));
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = grpId * 10 + 4,
            GroupId = grpId,
            WeekDayId = 1,
            LessonStartTimeId = 4,
            HoursCount = 2f,
            RoomId = 1,
            StartDate = startDate
        });
        return group;
    }

    //ჯგუფის საწყისი ბრძანება ისე, როგორც ვიზარდი 1-ის SaveChanges-ის შემდეგ: ბრძანებას Id orderId აქვს, ხაზებს
    //firstLineId-იდან ზედიზედ (მასწავლებლები, მოსწავლეები, განრიგი), სტრიქონებს კი მათი Id-ები კავშირში
    public static GroupOrder StoredInitialOrder(Group group, int orderId, int firstLineId)
    {
        GroupOrder order = GroupOrdersSwitchWriter.CreateInitialOrder(group,
            GroupOrdersSwitchPlanner.InitialOrder(group.GrpId, GroupStateMapper.ToState(group), Day(11, 20))!,
            "admin", Day(11, 20));
        order.Id = orderId;
        int lineId = firstLineId;
        foreach (GroupOrderTeacher line in order.GroupOrderTeachers)
        {
            line.Id = lineId++;
        }

        foreach (GroupOrderStudent line in order.GroupOrderStudents)
        {
            line.Id = lineId++;
        }

        foreach (GroupOrderDayTimePlace line in order.GroupOrderDayTimePlaces)
        {
            line.Id = lineId++;
        }

        foreach (GroupByTeacher row in group.GroupsByTeachers)
        {
            row.GroupOrderTeacherId = row.GroupOrderTeacher!.Id;
        }

        foreach (GroupByStudent row in group.GroupsByStudents)
        {
            row.GroupOrderStudentId = row.GroupOrderStudent!.Id;
        }

        foreach (GroupDayTimePlace row in group.GroupDayTimePlaces)
        {
            row.GroupOrderDayTimePlaceId = row.GroupOrderDayTimePlace!.Id;
        }

        return order;
    }

    public static GroupOrderLinkedRows LinkedRows(Group group)
    {
        return new GroupOrderLinkedRows([.. group.GroupsByTeachers], [.. group.GroupsByStudents],
            [.. group.GroupDayTimePlaces]);
    }

    private static GroupByStudent StudentRow(int gbsId, int grpId, int studentContractId, DateTime startDate,
        DateTime? endDate, string? note)
    {
        return new GroupByStudent
        {
            GbsId = gbsId,
            GroupId = grpId,
            StudentContractId = studentContractId,
            FourWeekHours = 8f,
            FourWeekFee = 48m,
            OneHourFee = 6m,
            HoursCoefficient = 1f,
            Note = note,
            StartDate = startDate,
            EndDate = endDate
        };
    }
}
