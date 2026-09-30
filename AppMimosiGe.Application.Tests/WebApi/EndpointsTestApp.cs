using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.StudentContracts.CreateStudentContract;
using AppMimosiGe.Application.StudentContracts.DeleteStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContract;
using AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;
using AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;
using AppMimosiGe.Application.StudentContracts.SearchHumans;
using AppMimosiGe.Application.StudentContracts.UpdateStudentContract;
using AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;
using AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContract;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;
using AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;
using AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;
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
        return builder.Build();
    }

    public static List<RouteEndpoint> MappedEndpoints(WebApplication app)
    {
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()];
    }
}
