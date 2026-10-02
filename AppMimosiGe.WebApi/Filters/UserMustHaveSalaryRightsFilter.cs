using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "ხელფასები" უჩანს, მას უწყისების ნახვა, შეცვლა, გამოთვლა და ფაილების
//მომზადება შეუძლია. ხელფასი მგრძნობიარეა, ამიტომ ეს პუნქტი მხოლოდ Admin-ს აქვს (D108)
public sealed class UserMustHaveSalaryRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "salary";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveSalaryRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveSalaryRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
