using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "დარიცხვები და გადახდები" უჩანს, მას ამონაწერის ნახვა შეუძლია
public sealed class UserMustHaveChargesAndPaymentsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "chargesAndPayments";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveChargesAndPaymentsRightsFilter(IUserRightsRepository repo,
        IDatabaseAbstraction databaseAbstraction, ILogger<UserMustHaveChargesAndPaymentsRightsFilter> logger,
        ICurrentUser currentUser) : base([MenuKey], repo, logger, currentUser, databaseAbstraction)
    {
    }
}
