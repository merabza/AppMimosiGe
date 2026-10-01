using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "გაკვეთილები" უჩანს, მას ყველა გაკვეთილის ნახვა და ჟურნალის
//შევსება შეუძლია (D71: "მხოლოდ ჩემი გაკვეთილები" ცალკე უფლებად არ არის)
public sealed class UserMustHaveLessonsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "lessons";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveLessonsRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveLessonsRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
