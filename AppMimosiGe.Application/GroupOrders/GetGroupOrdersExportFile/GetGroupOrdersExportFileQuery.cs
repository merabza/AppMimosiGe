using AppMimosiGe.Application.GroupOrders.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrdersExportFile;

//ვიზარდი 2-ის ფაილის გადმოწერა (D195): FileName ვიზარდი 2-ის პასუხიდან
public sealed record GetGroupOrdersExportFileQuery(string FileName) : IQuery<GroupOrdersExportFile>;
