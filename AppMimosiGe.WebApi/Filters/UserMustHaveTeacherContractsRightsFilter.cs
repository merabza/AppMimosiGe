using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "მასწავლებლების კონტრაქტები" უჩანს, მას ეს endpoint-ებიც შეუძლია
public sealed class UserMustHaveTeacherContractsRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "teacherContracts";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveTeacherContractsRightsFilter(IUserRightsRepository repo,
        IDatabaseAbstraction databaseAbstraction, ILogger<UserMustHaveTeacherContractsRightsFilter> logger,
        ICurrentUser currentUser) : base([MenuKey], repo, logger, currentUser, databaseAbstraction)
    {
    }
}
