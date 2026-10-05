using AppMimosiGe.WebApi.Endpoints.V1;
using Microsoft.AspNetCore.Routing;
using Serilog;

namespace AppMimosiGe.WebApi.DependencyInjection;

// ReSharper disable once UnusedType.Global
public static class AppMimosiGeApiDependencyInjection
{
    //Mimosi-ს ყველა endpoint-ის ჯგუფი აქ ემატება
    public static bool UseAppMimosiGeApi(this IEndpointRouteBuilder endpoints, ILogger? debugLogger)
    {
        debugLogger?.Information("{MethodName} Started", nameof(UseAppMimosiGeApi));

        endpoints.UseStudentContractsEndpoints(debugLogger);
        endpoints.UseTeacherContractsEndpoints(debugLogger);
        endpoints.UseGroupsEndpoints(debugLogger);
        endpoints.UseLessonGeneratorEndpoints(debugLogger);
        endpoints.UseLessonsEndpoints(debugLogger);
        endpoints.UsePaymentsEndpoints(debugLogger);
        endpoints.UseChargesAndPaymentsEndpoints(debugLogger);
        endpoints.UseDepositsEndpoints(debugLogger);
        endpoints.UseCrmCallsEndpoints(debugLogger);
        endpoints.UseWorkHoursEndpoints(debugLogger);
        endpoints.UseSalaryEndpoints(debugLogger);
        endpoints.UseAcademicYearEndpoints(debugLogger);
        endpoints.UseReportsEndpoints(debugLogger);

        debugLogger?.Information("{MethodName} Finished", nameof(UseAppMimosiGeApi));

        return true;
    }
}
