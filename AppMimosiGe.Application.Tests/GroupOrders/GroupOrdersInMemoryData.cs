using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Moq;
using SystemTools.Domain.Abstractions;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ბრძანებების InMemory სცენარების ბაზა: წელი 11 (2026–2027), ცნობარები, კონტრაქტები და ჯგუფები. მხოლოდ სინთეზური
//მონაცემები
internal static class GroupOrdersInMemoryData
{
    public static DbContextOptions<MimosiGeDbContext> NewDatabase()
    {
        return new DbContextOptionsBuilder<MimosiGeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    //carcass-ის SaveChangesAsync-ს domain events dispatcher სჭირდება, ამიტომ კონტექსტი სინქრონულად ინახება
    public static IUnitOfWork UnitOfWork(MimosiGeDbContext context)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => context.SaveChanges())
            .Returns(Task.CompletedTask);
        return unitOfWork.Object;
    }

    public static TimeProvider TimeProvider(DateTime now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(now, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
    }

    //წელი 11, საგანი 6, ზომა 2, სტატუსი 10 (Rate 106), სქემა 8, მასწავლებლები 5 და 6, ორშაბათი და ოთხშაბათი, დრო 4
    //(15:00), ოთახი 1, გენერატორის თვე 12.2026 და ჯგუფები თავისი მოსწავლეების კონტრაქტებით. ჯგუფის სტრიქონებს ხაზი
    //არ აქვს, როგორც ჯგუფების რეჟიმში
    public static void Seed(MimosiGeDbContext context, params Group[] groups)
    {
        context.AcademicYears.Add(new AcademicYear
        {
            AyId = 11, AcademicYearName = "2026-2027", StartDate = Day(9, 1), FinishDate = Day(9, 1).AddYears(1)
        });
        context.Courses.Add(new Course { CrsId = 6, CourseName = "მათემატიკა" });
        context.GroupSizes.Add(new GroupSize { GrsId = 2, GrsSize = 2, GrsName = "2" });
        context.StudentStatuses.Add(new StudentStatus { Id = 10, StudentStatusName = "მე-6 კლასი", Rate = 106 });
        context.TeacherSalarySchemes.Add(new TeacherSalaryScheme { Id = 8, SchemaName = "საათობრივი" });
        context.Humans.AddRange(Human(1), Human(2));
        context.TeacherContracts.AddRange(
            new TeacherContract { Id = 5, ContractNumber = "T5", TeacherHumanId = 1, SalarySchemaByHoursId = 8 },
            new TeacherContract { Id = 6, ContractNumber = "T6", TeacherHumanId = 2, SalarySchemaByHoursId = 8 });
        foreach (int studentContractId in groups.SelectMany(g => g.GroupsByStudents)
                     .Select(s => s.StudentContractId).Distinct())
        {
            context.Humans.Add(Human(studentContractId));
            context.StudentContracts.Add(new StudentContract
            {
                ScId = studentContractId,
                ContractNumber = $"6.0{studentContractId}",
                StudentHumanId = studentContractId,
                PayerHumanId = studentContractId,
                AcademicYearId = 11,
                DirtyNextPayDate = false
            });
        }

        context.WeekDays.AddRange(new WeekDay { Id = 1, Name = "ორშაბათი", ShortName = "ორშ", WeekDayNumber = 1 },
            new WeekDay { Id = 3, Name = "ოთხშაბათი", ShortName = "ოთხ", WeekDayNumber = 3 });
        context.LessonStartTimes.Add(new LessonStartTime { LstId = 4, LstTime = new TimeOnly(15, 0) });
        context.Rooms.Add(new Room { Id = 1, RoomName = "1" });
        context.OperationMonths.Add(new OperationMonth { MonthDate = Day(12, 1) });
        context.Groups.AddRange(groups);
        context.SaveChanges();
    }

    private static Human Human(int id)
    {
        return new Human { HumId = id, LastName = $"გვარი{id}", FirstName = "სახელი", PersonalId = $"010000000{id}" };
    }
}
