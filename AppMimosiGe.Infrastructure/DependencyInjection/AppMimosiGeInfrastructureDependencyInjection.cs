using AppMimosiGe.Application.AcademicYears;
using System;
using System.Net.Http;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.CrmCalls;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.Salary;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Application.WorkHours;
using AppMimosiGe.Infrastructure.BankTransactions.TbcBis;
using AppMimosiGe.Infrastructure.Reports;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGe.Infrastructure.Rights;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppMimosiGe.Infrastructure.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class AppMimosiGeInfrastructureDependencyInjection
{
    //tbcBisSettings: კონფიგურაციის სექცია "TbcBis" (ჰოსტი კითხულობს); null: ინტეგრაცია არ არის კონფიგურირებული
    public static IServiceCollection AddAppMimosiGeInfrastructure(this IServiceCollection services,
        ILogger? debugLogger, TbcBisSettings? tbcBisSettings = null)
    {
        debugLogger?.Information("{MethodName} Started", nameof(AddAppMimosiGeInfrastructure));

        services.AddScoped<IStudentContractsRepository, StudentContractsRepository>();
        services.AddScoped<ITeacherContractsRepository, TeacherContractsRepository>();
        services.AddScoped<IGroupsRepository, GroupsRepository>();
        services.AddScoped<ILessonGeneratorRepository, LessonGeneratorRepository>();
        services.AddScoped<ILessonsRepository, LessonsRepository>();
        services.AddScoped<IPaymentsRepository, PaymentsRepository>();
        services.AddScoped<IBalancesRepository, BalancesRepository>();
        services.AddScoped<ICrmCallsRepository, CrmCallsRepository>();
        services.AddScoped<IWorkHoursRepository, WorkHoursRepository>();
        services.AddScoped<ISalaryRepository, SalaryRepository>();
        services.AddScoped<IReportsRepository, ReportsRepository>();
        services.AddScoped<IAcademicYearsRepository, AcademicYearsRepository>();
        services.AddSingleton<IReportExcelWriter, OpenXmlReportExcelWriter>();
        services.AddScoped<IUserClaimRights, UserClaimRights>();
        services.AddScoped<IBankTransactionsRepository, BankTransactionsRepository>();

        //ერთი კლიენტი მთელი პროცესისთვის: ტოკენი ინახება და გამოიყენება, სანამ ვადა არ გასდის
        TbcBisSettings settings = tbcBisSettings ?? new TbcBisSettings();
        services.AddSingleton<IBankMovementsClient>(sp => new TbcBisClient(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
            {
                Timeout = TimeSpan.FromSeconds(60)
            }, settings, sp.GetRequiredService<TimeProvider>()));

        debugLogger?.Information("{MethodName} Finished", nameof(AddAppMimosiGeInfrastructure));

        return services;
    }
}
