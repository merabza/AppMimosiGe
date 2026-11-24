using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "ბრძანებები" უჩანს, მას ეს endpoint-ებიც შეუძლია (D184). წარსული თარიღის
//ბრძანებებს და საწყისი ბრძანების შეცვლას handler-ი AppClaim-ით ამოწმებს
public sealed class UserMustHaveGroupOrdersRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "groupOrders";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveGroupOrdersRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveGroupOrdersRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo,
        logger, currentUser, databaseAbstraction)
    {
    }
}
