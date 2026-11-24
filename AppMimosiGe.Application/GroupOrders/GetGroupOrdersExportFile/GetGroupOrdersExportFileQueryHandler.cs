using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrdersExportFile;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupOrdersExportFileQueryHandler(
    IGroupOrdersExportFiles exportFiles,
    GroupOrdersSettings settings) : IQueryHandler<GetGroupOrdersExportFileQuery, GroupOrdersExportFile>
{
    //სხვა სახელის ფაილი არ მოიძებნება, რომ ExportFolder-ის გარეთ არაფერი წაიკითხოს (ValidationDecorator query-ებს არ
    //ამოწმებს, ამიტომ აქ მოწმდება)
    public async Task<Result<GroupOrdersExportFile>> Handle(GetGroupOrdersExportFileQuery query,
        CancellationToken cancellationToken)
    {
        if (!GroupOrdersExportFile.IsFileName(query.FileName))
        {
            return GroupOrderErrors.ExportFileNotFound;
        }

        if (string.IsNullOrWhiteSpace(settings.ExportFolder))
        {
            return GroupOrderErrors.ExportFolderIsNotConfigured;
        }

        byte[]? content = await exportFiles.TryRead(query.FileName, cancellationToken);
        return content is null
            ? GroupOrderErrors.ExportFileNotFound
            : new GroupOrdersExportFile(query.FileName, content);
    }
}
