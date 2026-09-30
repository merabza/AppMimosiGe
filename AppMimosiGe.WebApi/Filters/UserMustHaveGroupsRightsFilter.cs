using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "ჯგუფები" უჩანს, მას ეს endpoint-ებიც შეუძლია
public sealed class UserMustHaveGroupsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "groups";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveGroupsRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveGroupsRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
