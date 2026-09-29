using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "მოსწავლეების კონტრაქტები" უჩანს, მას ეს endpoint-ებიც შეუძლია
public sealed class UserMustHaveStudentContractsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "studentContracts";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveStudentContractsRightsFilter(IUserRightsRepository repo,
        IDatabaseAbstraction databaseAbstraction, ILogger<UserMustHaveStudentContractsRightsFilter> logger,
        ICurrentUser currentUser) : base([MenuKey], repo, logger, currentUser, databaseAbstraction)
    {
    }
}
