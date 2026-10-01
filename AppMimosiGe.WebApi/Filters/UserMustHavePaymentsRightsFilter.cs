using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "გადახდები" უჩანს, მას გადახდების ნახვა, დამატება, შეცვლა და წაშლა
//შეუძლია. შემოწმებულ გადახდებს დამატებით გადახდების შემოწმების სპეციალური უფლება სჭირდება (handler-ები, D77)
public sealed class UserMustHavePaymentsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "payments";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHavePaymentsRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHavePaymentsRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
