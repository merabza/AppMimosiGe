using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.GetReportExcel;

//რეპორტი Excel-ის ფაილად (Access-ის FrmMain.cmdExcell)
public sealed record GetReportExcelQuery(string Key, ReportParametersRequest Parameters) : IQuery<ReportFile>;
