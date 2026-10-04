using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using Microsoft.Extensions.DependencyInjection;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports;

/// <summary>
///     კატალოგის რეპორტი: გასაღები (Access-ის ReportName; ამავე გასაღებით არის რეპორტის AppClaim), სათაური, აღწერა,
///     კატეგორიები ("ყველა"-ს გარდა, მასში ყოველი რეპორტია), პარამეტრები და მისი query handler-ის გამოძახება
/// </summary>
public sealed class ReportDefinition
{
    private ReportDefinition(string key, string title, string description, IReadOnlyList<string> categoryKeys,
        IReadOnlyList<ReportParameter> parameters,
        Func<ReportParametersRequest, IServiceProvider, CancellationToken, Task<Result<ReportTable>>> run)
    {
        Key = key;
        Title = title;
        Description = description;
        CategoryKeys = categoryKeys;
        Parameters = parameters;
        Run = run;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<string> CategoryKeys { get; }
    public IReadOnlyList<ReportParameter> Parameters { get; }

    //რეპორტის query handler-ის გამოძახება შემოწმებული პარამეტრებით
    public Func<ReportParametersRequest, IServiceProvider, CancellationToken, Task<Result<ReportTable>>> Run { get; }

    //TQuery რეპორტის ტიპიზებული query-ა; მის handler-ს DI იძლევა (Scrutor, დეკორატორებით)
    public static ReportDefinition Create<TQuery>(string key, string title, string description,
        IReadOnlyList<string> categoryKeys, IReadOnlyList<ReportParameter> parameters,
        Func<ReportParametersRequest, TQuery> createQuery) where TQuery : IQuery<ReportTable>
    {
        return new ReportDefinition(key, title, description, categoryKeys, parameters,
            (request, services, cancellationToken) => services.GetRequiredService<IQueryHandler<TQuery, ReportTable>>()
                .Handle(createQuery(request), cancellationToken));
    }
}
