using System;
using AppMimosiGe.Application.Salary.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.GetDeclarationFile;

//Month დეკლარაციის თვის ნებისმიერი დღეა
public sealed record GetDeclarationFileQuery(DateTime? Month) : IQuery<SalaryFile>;
