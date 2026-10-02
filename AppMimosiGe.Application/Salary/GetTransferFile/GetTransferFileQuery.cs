using AppMimosiGe.Application.Salary.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.GetTransferFile;

public sealed record GetTransferFileQuery(int ShId) : IQuery<SalaryFile>;
