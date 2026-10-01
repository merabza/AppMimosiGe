using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "CRM დარეკვები" უჩანს, მას ზარების ნახვა, დამატება, შეცვლა და
//წაშლა შეუძლია (ბალანსების გვერდის "ზარიც" ამ endpoint-ებს იყენებს)
public sealed class UserMustHaveCrmCallsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "crmCalls";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveCrmCallsRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveCrmCallsRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
