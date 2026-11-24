using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups.Models;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupStateWriterTests
{
    //ჯგუფი ბაზიდან: მასწავლებლის სტრიქონი 11 (ხაზი 101), მოსწავლის 21 (ხაზი 201) და განრიგის 31 (ხაზი 301)
    private static Group StoredGroup()
    {
        return new Group
        {
            GrpId = 7,
            GroupCode = "501",
            AcademicYearId = 10,
            CourseId = 1,
            GroupSizeId = 1,
            StudentStatusId = 1,
            DirtyLessons = false,
            GroupsByTeachers = [Teacher(11, 101)],
            GroupsByStudents = [Student(21, 201, 10)],
            GroupDayTimePlaces = [DayTimePlace(31, 301)]
        };
    }

    private static GroupByTeacher Teacher(int id, int? lineId)
    {
        return new GroupByTeacher
        {
            Id = id,
            GroupId = 7,
            TeacherContractId = 1,
            SalarySchemaId = 1,
            StartDate = Day(9, 1),
            GroupOrderTeacherId = lineId
        };
    }

    private static GroupByStudent Student(int id, int? lineId, int studentContractId)
    {
        return new GroupByStudent
        {
            GbsId = id,
            GroupId = 7,
            StudentContractId = studentContractId,
            StartDate = Day(9, 1),
            GroupOrderStudentId = lineId
        };
    }

    private static GroupDayTimePlace DayTimePlace(int id, int? lineId)
    {
        return new GroupDayTimePlace
        {
            GdtpId = id,
            GroupId = 7,
            WeekDayId = 1,
            LessonStartTimeId = 1,
            HoursCount = 2,
            RoomId = 1,
            StartDate = Day(9, 1),
            GroupOrderDayTimePlaceId = lineId
        };
    }

    private static GroupState StateOfLines(int teacherLineId, int studentLineId, int dayTimePlaceLineId)
    {
        return State(teachers: [TeacherRow(Day(9, 1), lineId: teacherLineId)],
            students: [StudentRow(Day(9, 1), lineId: studentLineId)],
            dayTimePlaces: [DayTimePlaceRow(Day(9, 1), lineId: dayTimePlaceLineId)]);
    }

    private static GroupOrderNewLines NewLines(GroupOrderTeacher teacher, GroupOrderStudent student,
        GroupOrderDayTimePlace dayTimePlace)
    {
        return new GroupOrderNewLines(new Dictionary<int, GroupOrderTeacher> { [-1] = teacher },
            new Dictionary<int, GroupOrderStudent> { [-2] = student },
            new Dictionary<int, GroupOrderDayTimePlace> { [-3] = dayTimePlace });
    }

    //სტრიქონი თავის ობიექტს (და Id-ს, რომელზეც გაკვეთილების მოსწავლეებია მიბმული) ინარჩუნებს და სამიზნის ველებს და
    //თარიღებს იღებს
    [Fact]
    public void Apply_RowOfAStateLine_KeepsItsEntityWithTheStateValues()
    {
        // Arrange
        Group group = StoredGroup();
        GroupByTeacher teacher = group.GroupsByTeachers.Single();
        GroupByStudent student = group.GroupsByStudents.Single();
        GroupDayTimePlace dayTimePlace = group.GroupDayTimePlaces.Single();
        var state = new GroupState(Fields, null, [new GroupTeacherRow(101, 6, 9, Day(9, 2), Day(12, 1))],
            [new GroupStudentRow(201, 21, 12f, 84m, 7m, 1.5f, "note", Day(9, 3), Day(11, 2))],
            [new GroupDayTimePlaceRow(301, 3, 5, 1.5f, 2, Day(9, 4), Day(10, 5))]);

        // Act
        GroupRemovedRows removed = GroupStateWriter.Apply(group, state, GroupOrderNewLines.None);

        // Assert
        Assert.Same(teacher, Assert.Single(group.GroupsByTeachers));
        Assert.Equal((11, (int?)101, 6, 9, Day(9, 2), (DateTime?)Day(12, 1)),
            (teacher.Id, teacher.GroupOrderTeacherId, teacher.TeacherContractId, teacher.SalarySchemaId,
                teacher.StartDate, teacher.EndDate));
        Assert.Same(student, Assert.Single(group.GroupsByStudents));
        Assert.Equal((21, (int?)201, 21, 12f, 84m, 7m, 1.5f, "note", Day(9, 3), (DateTime?)Day(11, 2)),
            (student.GbsId, student.GroupOrderStudentId, student.StudentContractId, student.FourWeekHours,
                student.FourWeekFee, student.OneHourFee, student.HoursCoefficient, student.Note, student.StartDate,
                student.EndDate));
        Assert.Same(dayTimePlace, Assert.Single(group.GroupDayTimePlaces));
        Assert.Equal((31, (int?)301, 3, 5, 1.5f, 2, Day(9, 4), (DateTime?)Day(10, 5)),
            (dayTimePlace.GdtpId, dayTimePlace.GroupOrderDayTimePlaceId, dayTimePlace.WeekDayId,
                dayTimePlace.LessonStartTimeId, dayTimePlace.HoursCount, dayTimePlace.RoomId, dayTimePlace.StartDate,
                dayTimePlace.EndDate));
        Assert.Empty(removed.Teachers);
        Assert.Empty(removed.Students);
        Assert.Empty(removed.DayTimePlaces);
    }

    //DirtyLessons-ს ჩამწერი ცალკე რთავს
    [Fact]
    public void Apply_WritesTheGroupFieldsAndVoidDate()
    {
        // Arrange
        Group group = StoredGroup();

        // Act
        GroupStateWriter.Apply(group, StateOfLines(101, 201, 301) with { VoidDate = Day(12, 20) },
            GroupOrderNewLines.None);

        // Assert
        Assert.Equal((7, 11, "601", 6, 2, 10, (DateTime?)Day(12, 20), false),
            (group.GrpId, group.AcademicYearId, group.GroupCode, group.CourseId, group.GroupSizeId,
                group.StudentStatusId, group.VoidDate, group.DirtyLessons));
    }

    //სამიზნე, რომელშიც გაუქმების თარიღი აღარ არის, ჯგუფს ხსნის
    [Fact]
    public void Apply_StateWithoutVoidDate_ClearsIt()
    {
        // Arrange
        Group group = StoredGroup();
        group.VoidDate = Day(12, 20);

        // Act
        GroupStateWriter.Apply(group, StateOfLines(101, 201, 301), GroupOrderNewLines.None);

        // Assert
        Assert.Null(group.VoidDate);
    }

    //სტრიქონი, რომლის ხაზიც სამიზნეში აღარ არის, და კავშირის გარეშე სტრიქონი მოხსნილია. ჯგუფის კოლექციაში ისინი
    //რჩება: მათ რეპოზიტორია შლის
    [Fact]
    public void Apply_RowsWithoutAStateLine_AreReturnedAsRemoved()
    {
        // Arrange
        Group group = StoredGroup();
        group.GroupsByTeachers.Add(Teacher(12, 102));
        group.GroupsByStudents.Add(Student(22, 202, 11));
        group.GroupsByStudents.Add(Student(23, null, 12));
        group.GroupDayTimePlaces.Add(DayTimePlace(32, 302));

        // Act
        GroupRemovedRows removed = GroupStateWriter.Apply(group, StateOfLines(101, 201, 301), GroupOrderNewLines.None);

        // Assert
        Assert.Equal([12], removed.Teachers.Select(x => x.Id));
        Assert.Equal([22, 23], removed.Students.Select(x => x.GbsId));
        Assert.Equal([32], removed.DayTimePlaces.Select(x => x.GdtpId));
        Assert.Equal([11, 12], group.GroupsByTeachers.Select(x => x.Id));
        Assert.Equal([21, 22, 23], group.GroupsByStudents.Select(x => x.GbsId));
        Assert.Equal([31, 32], group.GroupDayTimePlaces.Select(x => x.GdtpId));
    }

    //შესანახი ბრძანების ახალ ხაზს ახალი სტრიქონი navigation-ით უკავშირდება: ხაზის Id-ს SaveChanges ჩაწერს
    [Fact]
    public void Apply_RowOfANewLine_IsAddedAndLinkedToTheLine()
    {
        // Arrange
        Group group = StoredGroup();
        var teacherLine = new GroupOrderTeacher();
        var studentLine = new GroupOrderStudent();
        var dayTimePlaceLine = new GroupOrderDayTimePlace();
        GroupState state = State(teachers: [TeacherRow(Day(9, 1), lineId: 101), TeacherRow(Day(10, 1), lineId: -1)],
            students: [StudentRow(Day(9, 1), lineId: 201), StudentRow(Day(10, 2), Day(11, 2), -2)],
            dayTimePlaces: [DayTimePlaceRow(Day(9, 1), lineId: 301), DayTimePlaceRow(Day(10, 3), lineId: -3)]);

        // Act
        GroupRemovedRows removed = GroupStateWriter.Apply(group, state,
            NewLines(teacherLine, studentLine, dayTimePlaceLine));

        // Assert
        GroupByTeacher teacher = group.GroupsByTeachers.Last();
        Assert.Equal(2, group.GroupsByTeachers.Count);
        Assert.Same(teacherLine, teacher.GroupOrderTeacher);
        Assert.Equal((0, (int?)null, 5, 8, Day(10, 1), (DateTime?)null),
            (teacher.Id, teacher.GroupOrderTeacherId, teacher.TeacherContractId, teacher.SalarySchemaId,
                teacher.StartDate, teacher.EndDate));
        GroupByStudent student = group.GroupsByStudents.Last();
        Assert.Equal(2, group.GroupsByStudents.Count);
        Assert.Same(studentLine, student.GroupOrderStudent);
        Assert.Equal((0, (int?)null, 20, 8f, 48m, 6m, 1f, Day(10, 2), (DateTime?)Day(11, 2)),
            (student.GbsId, student.GroupOrderStudentId, student.StudentContractId, student.FourWeekHours,
                student.FourWeekFee, student.OneHourFee, student.HoursCoefficient, student.StartDate,
                student.EndDate));
        GroupDayTimePlace dayTimePlace = group.GroupDayTimePlaces.Last();
        Assert.Equal(2, group.GroupDayTimePlaces.Count);
        Assert.Same(dayTimePlaceLine, dayTimePlace.GroupOrderDayTimePlace);
        Assert.Equal((0, (int?)null, 1, 4, 2f, 1, Day(10, 3)),
            (dayTimePlace.GdtpId, dayTimePlace.GroupOrderDayTimePlaceId, dayTimePlace.WeekDayId,
                dayTimePlace.LessonStartTimeId, dayTimePlace.HoursCount, dayTimePlace.RoomId,
                dayTimePlace.StartDate));
        Assert.Empty(removed.Teachers);
        Assert.Empty(removed.Students);
        Assert.Empty(removed.DayTimePlaces);
    }

    //ბაზაში შენახული ხაზი, რომელსაც ჯგუფში ჯერ სტრიქონი არ აქვს (მაგ. ბრძანება 8, რომელიც ადრეული ფასის ცვლილების
    //შემდეგ სტრიქონს ყოფს), ახალ სტრიქონს თავის Id-ს აძლევს
    [Fact]
    public void Apply_RowOfAStoredLineWithoutARow_IsAddedWithTheLineId()
    {
        // Arrange
        Group group = StoredGroup();
        GroupState state = State(teachers: [TeacherRow(Day(9, 1), lineId: 101)],
            students: [StudentRow(Day(9, 1), Day(10, 1), 201), StudentRow(Day(10, 1), lineId: 205)],
            dayTimePlaces: [DayTimePlaceRow(Day(9, 1), lineId: 301)]);

        // Act
        GroupStateWriter.Apply(group, state, GroupOrderNewLines.None);

        // Assert
        Assert.Equal([(21, (int?)201, (DateTime?)Day(10, 1)), (0, 205, null)],
            group.GroupsByStudents.Select(x => (x.GbsId, x.GroupOrderStudentId, x.EndDate)));
        Assert.Null(group.GroupsByStudents.Last().GroupOrderStudent);
    }

    //შექმნის ბრძანების ჯგუფი: ველები და ყველა სტრიქონი სამიზნიდან, თავის ხაზებთან კავშირით
    [Fact]
    public void Create_ReturnsANewGroupWithEveryRowOfTheState()
    {
        // Arrange
        var teacherLine = new GroupOrderTeacher();
        var studentLine = new GroupOrderStudent();
        var dayTimePlaceLine = new GroupOrderDayTimePlace();
        GroupState state = StateOfLines(-1, -2, -3);

        // Act
        Group group = GroupStateWriter.Create(state, NewLines(teacherLine, studentLine, dayTimePlaceLine));

        // Assert
        Assert.Equal((0, 11, "601", 6, 2, 10, (DateTime?)null, true),
            (group.GrpId, group.AcademicYearId, group.GroupCode, group.CourseId, group.GroupSizeId,
                group.StudentStatusId, group.VoidDate, group.DirtyLessons));
        Assert.Same(teacherLine, Assert.Single(group.GroupsByTeachers).GroupOrderTeacher);
        Assert.Same(studentLine, Assert.Single(group.GroupsByStudents).GroupOrderStudent);
        Assert.Same(dayTimePlaceLine, Assert.Single(group.GroupDayTimePlaces).GroupOrderDayTimePlace);
        Assert.Equal(Day(9, 1), group.GroupsByStudents.Single().StartDate);
    }
}
