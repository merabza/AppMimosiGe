using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.GetTransferFile;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetTransferFileQueryHandler(ISalaryRepository repository)
    : IQueryHandler<GetTransferFileQuery, SalaryFile>
{
    //Access-ის cmdCreateTransferFile + ExportTransferFile: ფაილის სახელი გადარიცხვის თარიღითაა
    public async Task<Result<SalaryFile>> Handle(GetTransferFileQuery request, CancellationToken cancellationToken)
    {
        SalaryHeader? header = await repository.GetHeaderForChange(request.ShId, cancellationToken);
        if (header is null)
        {
            return SalaryErrors.SalaryHeaderNotFound;
        }

        List<TransferFileRow> rows = await repository.GetTransferFileRows(request.ShId, cancellationToken);
        return new SalaryFile(SalaryFilesGenerator.TransferFileName(header.ShTransferDate),
            SalaryFilesGenerator.TransferFile(rows));
    }
}
