using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "სამუშაო საათების შესრულება" უჩანს, მას ჩანაწერების ნახვა, შეცვლა,
//სამუშაოს დაწყების/დასრულების დაფიქსირება და ავტომატური დაგენერირება შეუძლია
public sealed class UserMustHaveWorkHoursRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "workHours";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveWorkHoursRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveWorkHoursRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
