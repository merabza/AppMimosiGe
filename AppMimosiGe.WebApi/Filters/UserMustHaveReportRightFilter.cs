using System.Collections.Generic;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Rights;
using BackendCarcassShared.Contracts.Errors;
using Microsoft.AspNetCore.Http;

namespace AppMimosiGe.WebApi.Filters;

//რეპორტის უფლება: AppClaim, რომლის გასაღები რეპორტის გასაღებია (D112); უფლების გარეშე 403, როგორც carcass-ის
//ფილტრებში. უცნობ გასაღებს ფილტრი უშვებს, handler კი 404-ს აბრუნებს
public sealed class UserMustHaveReportRightFilter(IUserClaimRights claimRights) : IEndpointFilter
{
    public const string KeyRouteValue = "key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ReportDefinition? definition = context.HttpContext.Request.RouteValues[KeyRouteValue] is string key
            ? ReportsCatalog.Find(key)
            : null;
        if (definition is not null)
        {
            IReadOnlySet<string> claims = await claimRights.GetClaims(context.HttpContext.RequestAborted);
            if (!claims.Contains(definition.Key))
            {
                return Results.Json(new[] { RightsApiErrors.InsufficientRights },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return await next(context);
    }
}
