using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;
using AppMimosiGe.Application.AcademicYears.CreateAcademicYear;
using AppMimosiGe.Application.AcademicYears.GetAcademicYears;
using AppMimosiGe.Application.AcademicYears.GetAcademicYearWizardInfo;
using AppMimosiGe.Application.Balances.GetBalancesFormLookups;
using AppMimosiGe.Application.Balances.GetDeposits;
using AppMimosiGe.Application.Balances.GetStatement;
using AppMimosiGe.Application.Balances.GetStatementStudentContracts;
using AppMimosiGe.Application.Balances.RecountBalances;
using AppMimosiGe.Application.CrmCalls.CreateCrmCall;
using AppMimosiGe.Application.CrmCalls.DeleteCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCall;
using AppMimosiGe.Application.CrmCalls.GetCrmCallFormLookups;
using AppMimosiGe.Application.CrmCalls.GetCrmCallsRowsData;
using AppMimosiGe.Application.CrmCalls.GetCrmCallStudentContracts;
using AppMimosiGe.Application.CrmCalls.UpdateCrmCall;
using AppMimosiGe.Application.Groups.CreateGroup;
using AppMimosiGe.Application.Groups.DeleteGroup;
using AppMimosiGe.Application.Groups.GetGroup;
using AppMimosiGe.Application.Groups.GetGroupFormLookups;
using AppMimosiGe.Application.Groups.GetGroupsRowsData;
using AppMimosiGe.Application.Groups.GetGroupStudentContracts;
using AppMimosiGe.Application.Groups.UpdateGroup;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLastLesson;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLessons;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;
using AppMimosiGe.Application.LessonGenerator.GetLessonGeneratorLog;
using AppMimosiGe.Application.Lessons.GetLesson;
using AppMimosiGe.Application.Lessons.GetLessonFormLookups;
using AppMimosiGe.Application.Lessons.GetLessonsRowsData;
using AppMimosiGe.Application.Lessons.UpdateLesson;
using AppMimosiGe.Application.Payments.CreatePayment;
using AppMimosiGe.Application.Payments.DeletePayment;
using AppMimosiGe.Application.Payments.GetPayment;
using AppMimosiGe.Application.Payments.GetPaymentFormLookups;
using AppMimosiGe.Application.Payments.GetPaymentsRowsData;
using AppMimosiGe.Application.Payments.GetPaymentStudentContracts;
using AppMimosiGe.Application.Payments.UpdatePayment;
using AppMimosiGe.Application.Reports.GetReportCatalog;
using AppMimosiGe.Application.Reports.GetReportExcel;
using AppMimosiGe.Application.Reports.GetReportLookups;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.RunReport;
using AppMimosiGe.Application.Salary.CountSalary;
using AppMimosiGe.Application.Salary.CreateSalaryHeader;
using AppMimosiGe.Application.Salary.CreateSalaryPart;
using AppMimosiGe.Application.Salary.DeleteSalaryHeader;
using AppMimosiGe.Application.Salary.DeleteSalaryPart;
using AppMimosiGe.Application.Salary.GetDeclarationFile;
using AppMimosiGe.Application.Salary.GetSalaryFormLookups;
using AppMimosiGe.Application.Salary.GetSalaryHeader;
using AppMimosiGe.Application.Salary.GetSalaryHeaders;
using AppMimosiGe.Application.Salary.GetTransferFile;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGe.Application.Salary.UpdateSalaryHeader;
using AppMimosiGe.Application.Salary.UpdateSalaryPart;
using AppMimosiGe.Application.StudentContracts.CreateStudentContract;
using AppMimosiGe.Application.StudentContracts.DeleteStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;
using AppMimosiGe.Application.StudentContracts.GetStudentContractNextNumber;
using AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.StudentContracts.UpdateStudentContract;
using AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;
using AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;
using AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;
using AppMimosiGe.Application.WorkHours.CreateWorkHour;
using AppMimosiGe.Application.WorkHours.DeleteWorkHour;
using AppMimosiGe.Application.WorkHours.EndWork;
using AppMimosiGe.Application.WorkHours.GetWorkHour;
using AppMimosiGe.Application.WorkHours.GetWorkHourFormLookups;
using AppMimosiGe.Application.WorkHours.GetWorkHoursRowsData;
using AppMimosiGe.Application.WorkHours.StartWork;
using AppMimosiGe.Application.WorkHours.UpdateWorkHour;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Tests.WebApi;

//an app to map the Mimosi endpoints on, to check their routes
internal static class EndpointsTestApp
{
    public static WebApplication Build()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        //handlers are services; without them minimal APIs would bind them from the body
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<SearchHumansQuery, List<LookupItemResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetStudentContractQuery, StudentContractResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateStudentContractCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateStudentContractCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteStudentContractCommand>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetTeacherContractFormLookupsQuery, TeacherContractFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetTeacherContractQuery, TeacherContractResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateTeacherContractCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateTeacherContractCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteTeacherContractCommand>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetGroupQuery, GroupResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateGroupCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateGroupCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteGroupCommand>>());
        builder.Services.AddSingleton(
            Mock.Of<ICommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse>>());
        builder.Services.AddSingleton(
            Mock.Of<ICommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse>>());
        builder.Services.AddSingleton(
            Mock.Of<ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetLessonGeneratorLogQuery, List<LessonGeneratorLogRowResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetLessonQuery, LessonResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateLessonCommand>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse>>());
        builder.Services.AddSingleton(
            Mock.Of<IQueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetPaymentQuery, PaymentResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreatePaymentCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdatePaymentCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeletePaymentCommand>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetStatementQuery, StatementRowsDataResponse>>());
        builder.Services.AddSingleton(
            Mock.Of<IQueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetDepositsQuery, DepositsResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetCrmCallFormLookupsQuery, CrmCallFormLookupsResponse>>());
        builder.Services.AddSingleton(
            Mock.Of<IQueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetCrmCallQuery, CrmCallResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateCrmCallCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateCrmCallCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteCrmCallCommand>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse>>());
        builder.Services.AddSingleton(
            Mock.Of<IQueryHandler<GetWorkHourFormLookupsQuery, WorkHourFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetWorkHourQuery, WorkHourResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateWorkHourCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateWorkHourCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteWorkHourCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<StartWorkCommand, WorkHourResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<EndWorkCommand, WorkHourResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<ICommandHandler<AutoGenerateWorkHoursCommand, WorkHoursAutoGenerateResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetSalaryHeadersQuery, List<SalaryHeaderRowResponse>>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetSalaryFormLookupsQuery, SalaryFormLookupsResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetSalaryHeaderQuery, SalaryHeaderResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateSalaryHeaderCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateSalaryHeaderCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteSalaryHeaderCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateSalaryPartCommand, int>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<UpdateSalaryPartCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<DeleteSalaryPartCommand>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CountSalaryCommand, SalaryCountResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetTransferFileQuery, SalaryFile>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetDeclarationFileQuery, SalaryFile>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetReportCatalogQuery, ReportCatalogResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetReportLookupsQuery, ReportLookupsResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<RunReportQuery, ReportResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetReportExcelQuery, ReportFile>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetStudentContractNextNumberQuery, StudentContractNextNumberResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<IQueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse>>());
        builder.Services.AddSingleton(Mock.Of<ICommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse>>());
        builder.Services.AddSingleton(Mock.Of<IQueryHandler<GetAcademicYearsQuery, AcademicYearsResponse>>());
        builder.Services.AddSingleton(Mock
            .Of<ICommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>>());
        return builder.Build();
    }

    public static List<RouteEndpoint> MappedEndpoints(WebApplication app)
    {
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()];
    }
}
