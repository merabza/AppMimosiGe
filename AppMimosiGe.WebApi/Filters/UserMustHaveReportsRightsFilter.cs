using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "რეპორტები" უჩანს, მას რეპორტების გვერდი (კატალოგი, ფილტრების
//სიები) ეხსნება. თითო რეპორტს დამატებით საკუთარი AppClaim სჭირდება (UserMustHaveReportRightFilter, D112)
public sealed class UserMustHaveReportsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "reports";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveReportsRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveReportsRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
