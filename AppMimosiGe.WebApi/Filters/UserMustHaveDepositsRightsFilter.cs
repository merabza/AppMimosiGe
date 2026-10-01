using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "დეპოზიტები" უჩანს, მას ბალანსების ნახვა და გახსნისას dirty
//ჯგუფებისა და კონტრაქტების გადათვლა შეუძლია. სრულ გადაანგარიშებას სპეციალური უფლებაც სჭირდება
public sealed class UserMustHaveDepositsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "deposits";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveDepositsRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveDepositsRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
