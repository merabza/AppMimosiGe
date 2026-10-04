using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Groups.Models;

/// <summary>
///     ჯგუფების დეტალური სია თარიღისთვის (r10)
/// </summary>
public static class GroupListReport
{
    private const string DateFormat = "dd.MM.yyyy";

    //r10Groups (vR10Groups): აქტიური ჯგუფები მოქმედი მასწავლებლებით და მოსწავლეებით. ფილტრები არასავალდებულოა
    //(Access-ის query-ში ცარიელი ფილტრი = ყველა): მასწავლებელი მასწავლებლის სტრიქონებს ფილტრავს, საგანი ჯგუფებს,
    //მოსწავლე მოსწავლის სტრიქონებს; ჯგუფი, რომელსაც ფილტრის შემდეგ მასწავლებელი ან მოსწავლე არ დარჩა, არ ჩანს
    //(Access-ის INNER JOIN). სექცია = ჯგუფი კოდით: საგანი, მოსწავლეების სტატუსი და ყველა მასწავლებელი სქემითა და
    //დაწყებით (Access ჯგუფის ორ მასწავლებელზე მოსწავლეებს ორჯერ აჩვენებდა, სათაურში კი ერთ მასწავლებელს);
    //სტრიქონები: მოსწავლეები სახელით
    public static ReportTable Groups(GroupsSnapshot snapshot, int? teacherContractId, int? courseId,
        int? studentContractId)
    {
        ILookup<int, ReportGroupTeacher> teachers = snapshot.Teachers
            .Where(t => teacherContractId is null || t.TeacherContractId == teacherContractId).ToLookup(t => t.GroupId);
        ILookup<int, ReportGroupStudent> students = snapshot.Students
            .Where(s => studentContractId is null || s.StudentContractId == studentContractId)
            .ToLookup(s => s.GroupId);
        return new ReportTable(
        [
            new ReportColumnResponse("student", "მოსწავლე", ReportColumnTypes.Text),
            new ReportColumnResponse("fourWeekHours", "4 კვირის საათები", ReportColumnTypes.Number),
            new ReportColumnResponse("fourWeekFee", "4 კვირის გადასახადი", ReportColumnTypes.Number),
            new ReportColumnResponse("hoursCoefficient", "საათის კოეფიციენტი", ReportColumnTypes.Number),
            new ReportColumnResponse("startDate", "თარიღიდან", ReportColumnTypes.Date)
        ], [
            .. snapshot.Groups.Where(g =>
                    (courseId is null || g.CourseId == courseId) && teachers[g.GroupId].Any() &&
                    students[g.GroupId].Any()).OrderBy(g => g.GroupCode, StringComparer.Ordinal)
                .ThenBy(g => g.GroupId).Select(g => new ReportSectionResponse(
                    Header(snapshot, g, teachers[g.GroupId]), [
                        .. students[g.GroupId]
                            .Select(s => (Name: snapshot.StudentNames[s.StudentContractId].NameWithNumber, Row: s))
                            .OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.Row.GbsId).Select(s =>
                                new List<object?>
                                {
                                    s.Name, s.Row.FourWeekHours, s.Row.FourWeekFee, s.Row.HoursCoefficient,
                                    s.Row.StartDate
                                })
                    ], null))
        ], []);
    }

    //"ჯგუფი: … · საგანი: … · მოსწავლის სტატუსი: … · მასწავლებელი: გვარი სახელი / ნომერი, სქემა, თარიღიდან: …";
    //რამდენიმე მასწავლებელი "; "-ით, სახელით და დაწყებით
    private static string Header(GroupsSnapshot snapshot, ReportGroup group, IEnumerable<ReportGroupTeacher> teachers)
    {
        string teacherText = string.Join("; ", teachers
            .Select(t => (Name: snapshot.TeacherNames[t.TeacherContractId].NameWithNumber, Row: t))
            .OrderBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.Row.StartDate).ThenBy(t => t.Row.Id)
            .Select(t =>
                $"{t.Name}, {t.Row.SalarySchemeName}, თარიღიდან: {t.Row.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture)}"));
        return $"ჯგუფი: {group.GroupCode} · საგანი: {group.CourseName} · მოსწავლის სტატუსი: " +
               $"{group.StudentStatusName} · მასწავლებელი: {teacherText}";
    }
}
