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

        debugLogger?.Information("{MethodName} Finished", nameof(UseAppMimosiGeApi));

        return true;
    }
}
