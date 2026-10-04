using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.GetDeclarationFile;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetDeclarationFileQueryHandler(ISalaryRepository repository)
    : IQueryHandler<GetDeclarationFileQuery, SalaryFile>
{
    //Access-ის ExportTaxDepDeclarationFile: ყველა უწყისის სტრიქონი, რომლის გადარიცხვის თარიღი ამ თვეშია.
    //ValidationDecorator მხოლოდ ბრძანებებს ამოწმებს, ამიტომ თვე აქ მოწმდება
    public async Task<Result<SalaryFile>> Handle(GetDeclarationFileQuery request, CancellationToken cancellationToken)
    {
        if (request.Month is not { } month)
        {
            return SalaryErrors.DeclarationMonthIsRequired;
        }

        var from = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        List<DeclarationFileRow> rows =
            await repository.GetDeclarationFileRows(from, from.AddMonths(1), cancellationToken);
        return new SalaryFile(SalaryFilesGenerator.DeclarationFileName(from),
            SalaryFilesGenerator.DeclarationFile(rows));
    }
}
