using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "ბანკის ჩარიცხვები" უჩანს, მას ჩარიცხვების ნახვა, ბანკიდან მიღება და
//განაწილება შეუძლია
public sealed class UserMustHaveBankTransactionsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "bankTransactions";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveBankTransactionsRightsFilter(IUserRightsRepository repo,
        IDatabaseAbstraction databaseAbstraction, ILogger<UserMustHaveBankTransactionsRightsFilter> logger,
        ICurrentUser currentUser) : base([MenuKey], repo, logger, currentUser, databaseAbstraction)
    {
    }
}
