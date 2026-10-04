using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.Reports.Groups.Models;

/// <summary>
///     ჯგუფების შევსება თარიღისთვის: შეუვსებელი ჯგუფები (r08), გაერთიანების წყვილები (r09), ზომების ანალიზი (r23) და
///     ოპტიმიზაციის შესაძლებლობა (r24). ჯგუფის მოსწავლეები ამ დღეს მოქმედი მოსწავლის სტრიქონებია (Access-ის
///     Count(GroupsByStudents.ID): ერთი კონტრაქტის ორი სტრიქონი ორად ითვლება)
/// </summary>
public static class GroupFillReports
{
    private const string GroupsCountCaption = "ჯგუფების რაოდენობა:";
    private const string AverageLoadCaption = "საშუალო დატვირთვა:";
    private const string AverageFillCaption = "შევსების % სშ:";

    //r08LessSizeGroups (vR08LessSizeGroups): აქტიური ჯგუფები, რომელთა მოსწავლეები ადგილებზე (ზომაზე) ნაკლებია.
    //სექცია = ჯგუფის ზომა (Access-ის რეპორტის დაჯგუფება GroupSizeID-ით), სტრიქონები ჯგუფის კოდით
    public static ReportTable LessSizeGroups(GroupsSnapshot snapshot)
    {
        return new ReportTable(
        [
            Text("groupCode", "ჯგუფი"), Text("course", "საგანი"), Text("studentStatus", "მოსწავლის სტატუსი"),
            WholeNumber("studentsCount", "რაოდენობა")
        ], [
            .. BySize(UnderFilled(snapshot)).Select(size => new ReportSectionResponse(size[0].Group.GroupSizeName, [
                .. size.OrderBy(f => f.Group.GroupCode, StringComparer.Ordinal).ThenBy(f => f.Group.GroupId).Select(f =>
                    new List<object?>
                    {
                        f.Group.GroupCode, f.Group.CourseName, f.Group.StudentStatusName, f.StudentsCount
                    })
            ], null))
        ], []);
    }

    //r09Optimization (vR09Optimization): ერთი საგნის, სტატუსისა და ზომის შეუვსებელი ჯგუფების წყვილები, რომელთა
    //მოსწავლეები ერთ ჯგუფში ეტევა. წყვილი ერთხელ ჩანს, პირველი ჯგუფი კოდით (და ID-ით) წინაა (Access ორივე რიგით
    //აჩვენებდა). რიგი: პირველი ჯგუფი, მეორე ჯგუფი (Access-ის რეპორტის დალაგება)
    public static ReportTable Optimization(GroupsSnapshot snapshot)
    {
        List<GroupFill> underFilled =
        [
            .. UnderFilled(snapshot).OrderBy(f => f.Group.GroupCode, StringComparer.Ordinal)
                .ThenBy(f => f.Group.GroupId)
        ];
        return ReportTable.Flat([
            Text("firstGroupCode", "პირველი ჯგუფი"), Text("secondGroupCode", "მეორე ჯგუფი"),
            WholeNumber("size", "ზომა"), WholeNumber("studentsInFirstGroup", "მოსწავლეები პირველ ჯგუფში"),
            WholeNumber("studentsInSecondGroup", "მოსწავლეები მეორე ჯგუფში")
        ], [
            .. underFilled.SelectMany((first, index) => underFilled.Skip(index + 1)
                .Where(second => SameKind(first.Group, second.Group) &&
                                 first.Group.Size >= first.StudentsCount + second.StudentsCount)
                .Select(second => new List<object?>
                {
                    first.Group.GroupCode,
                    second.Group.GroupCode,
                    first.Group.Size,
                    first.StudentsCount,
                    second.StudentsCount
                }))
        ]);
    }

    //r23GroupSizesAnalize (vR23GroupSizesAnalize): ყველა აქტიური ჯგუფი მოსწავლეებით და შევსებით (მოსწავლეები / ზომა).
    //სექცია = ზომა, სტრიქონები Access-ის რეპორტის დალაგებით: მოსწავლეები, სტატუსი, საგანი (შემდეგ ჯგუფის კოდი; ერთი
    //კოდის ჯგუფები აქ ერთნაირად იბეჭდება).
    //ბოლოს Access-ის რეპორტის ჯამები: ჯგუფების რაოდენობა, საშუალო დატვირთვა (მოსწავლეები / ჯგუფები) და შევსება
    //(მოსწავლეები / ადგილები)
    public static ReportTable GroupSizesAnalysis(GroupsSnapshot snapshot)
    {
        List<GroupFill> fills = Fills(snapshot);
        List<ReportColumnResponse> columns =
        [
            Text("groupCode", "ჯგუფი"), Text("course", "საგანი"), Text("studentStatus", "მოსწავლის სტატუსი"),
            WholeNumber("studentsCount", "რაოდენობა"), Text("fill", "შევსების %")
        ];
        int students = fills.Sum(f => f.StudentsCount);
        return new ReportTable(columns, [
            .. BySize(fills).Select(size => new ReportSectionResponse(size[0].Group.GroupSizeName, [
                .. size.OrderBy(f => f.StudentsCount).ThenBy(f => f.Group.StudentStatusName, StringComparer.Ordinal)
                    .ThenBy(f => f.Group.CourseName, StringComparer.Ordinal)
                    .ThenBy(f => f.Group.GroupCode, StringComparer.Ordinal).Select(f =>
                        new List<object?>
                        {
                            f.Group.GroupCode,
                            f.Group.CourseName,
                            f.Group.StudentStatusName,
                            f.StudentsCount,
                            Percent(f.StudentsCount, f.Group.Size, 0)
                        })
            ], null))
        ], [
            Footer(columns, GroupsCountCaption, fills.Count),
            Footer(columns, AverageLoadCaption, fills.Count == 0 ? null : Fixed((decimal)students / fills.Count)),
            Footer(columns, AverageFillCaption, Percent(students, fills.Sum(f => f.Group.Size), 2))
        ]);
    }

    //r24GroupsOptimization (vR24Base1LessGroups, vR24Base2, vR24GroupsOptimization): შეუვსებელი ჯგუფები იმ (საგანი,
    //სტატუსი, ზომა) კომბინაციებში, სადაც ჯგუფები მეტია, ვიდრე მოსწავლეებს სჭირდება (ჯგუფები > ⌈მოსწავლეები / ზომა⌉).
    //სექცია = კომბინაცია თავისი ჯამებით (Access-ში სექცია მხოლოდ საგანი იყო და ორ კომბინაციას პირველის ჯამები
    //ეწერა); სტრიქონი: ჯგუფი, მისი მასწავლებლები, მოსწავლე, თითო მოსწავლე ერთხელ (Access ჯგუფის ორ მასწავლებელზე
    //მოსწავლეებს ორჯერ აჩვენებდა). რიგი: საგანი, სტატუსი, ზომა; ჯგუფი, მოსწავლე
    public static ReportTable GroupsOptimization(GroupsSnapshot snapshot)
    {
        ILookup<int, ReportGroupStudent> students = snapshot.Students.ToLookup(s => s.GroupId);
        return new ReportTable(
        [
            Text("groupCode", "ჯგუფი"), Text("teachers", "მასწავლებელი"), Text("student", "მოსწავლე")
        ], [
            .. UnderFilled(snapshot).GroupBy(f => (f.Group.CourseId, f.Group.StudentStatusId, f.Group.GroupSizeId))
                .Select(kind => kind.ToList())
                .Where(kind => kind.Count > MinimumGroups(kind.Sum(f => f.StudentsCount), kind[0].Group.Size))
                .OrderBy(kind => kind[0].Group.CourseName, StringComparer.Ordinal)
                .ThenBy(kind => kind[0].Group.CourseId)
                .ThenBy(kind => kind[0].Group.StudentStatusName, StringComparer.Ordinal)
                .ThenBy(kind => kind[0].Group.StudentStatusId).ThenBy(kind => kind[0].Group.GroupSizeId)
                .Select(kind => new ReportSectionResponse(
                    $"{kind[0].Group.CourseName} · {kind[0].Group.StudentStatusName} · {kind[0].Group.GroupSizeName}" +
                    $" · ჯგუფები: {kind.Count} · მოსწავლეები: {kind.Sum(f => f.StudentsCount)}", [
                        .. kind.OrderBy(f => f.Group.GroupCode, StringComparer.Ordinal).ThenBy(f => f.Group.GroupId)
                            .SelectMany(f => GroupStudentRows(snapshot, f.Group, students[f.Group.GroupId]))
                    ], null))
        ], []);
    }

    //ჯგუფის მასწავლებლები "გვარი სახელი / ნომერი"-ით, "; "-ით (ერთი მასწავლებლის ორი სტრიქონი ერთხელ)
    private static string TeacherNames(GroupsSnapshot snapshot, IEnumerable<ReportGroupTeacher> teachers)
    {
        return string.Join("; ",
            teachers.Select(t => snapshot.TeacherNames[t.TeacherContractId].NameWithNumber).Distinct()
                .Order(StringComparer.Ordinal));
    }

    //r24-ის სტრიქონები: თითო მოსწავლის სტრიქონი ჯგუფის კოდით და მასწავლებლებით, მოსწავლის სახელით დალაგებული
    //(ერთი სახელის სტრიქონები ერთნაირად იბეჭდება)
    private static IEnumerable<List<object?>> GroupStudentRows(GroupsSnapshot snapshot, ReportGroup group,
        IEnumerable<ReportGroupStudent> students)
    {
        string teachers = TeacherNames(snapshot, snapshot.Teachers.Where(t => t.GroupId == group.GroupId));
        return students.Select(s => snapshot.StudentNames[s.StudentContractId].NameWithNumber)
            .Order(StringComparer.Ordinal).Select(name => new List<object?> { group.GroupCode, teachers, name });
    }

    //ყველა აქტიური ჯგუფი მოსწავლეების რაოდენობით
    private static List<GroupFill> Fills(GroupsSnapshot snapshot)
    {
        ILookup<int, ReportGroupStudent> students = snapshot.Students.ToLookup(s => s.GroupId);
        return [.. snapshot.Groups.Select(g => new GroupFill(g, students[g.GroupId].Count()))];
    }

    //Access-ის HAVING Count(GroupsByStudents.ID) < Size
    private static IEnumerable<GroupFill> UnderFilled(GroupsSnapshot snapshot)
    {
        return Fills(snapshot).Where(f => f.StudentsCount < f.Group.Size);
    }

    //სექციები ზომის მიხედვით, GroupSizeID-ის რიგით
    private static IEnumerable<List<GroupFill>> BySize(IEnumerable<GroupFill> fills)
    {
        return fills.GroupBy(f => f.Group.GroupSizeId).OrderBy(size => size.Key).Select(size => size.ToList());
    }

    private static bool SameKind(ReportGroup first, ReportGroup second)
    {
        return first.StudentStatusId == second.StudentStatusId && first.CourseId == second.CourseId &&
               first.GroupSizeId == second.GroupSizeId;
    }

    //Access-ის -Int(-Sum(StudentsCountInGroup) / Size): რამდენი ჯგუფი სჭირდება მოსწავლეებს
    private static int MinimumGroups(int students, int size)
    {
        return (students + size - 1) / size;
    }

    //Access-ის Percent ფორმატი decimals ათწილადით; 0 ადგილზე პროცენტი არ ითვლება
    private static string? Percent(int count, int total, int decimals)
    {
        return total == 0
            ? null
            : Math.Round(count * 100m / total, decimals, MidpointRounding.AwayFromZero)
                .ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";
    }

    //Access-ის Fixed ფორმატი: 2 ათწილადი
    private static string Fixed(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);
    }

    //ჯამის სტრიქონი: წარწერა პირველ სვეტში, მნიშვნელობა მეორეში
    private static List<object?> Footer(List<ReportColumnResponse> columns, string caption,
        object? value)
    {
        return [caption, value, .. Enumerable.Repeat<object?>(null, columns.Count - 2)];
    }

    private static ReportColumnResponse Text(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.Text);
    }

    private static ReportColumnResponse WholeNumber(string name, string caption)
    {
        return new ReportColumnResponse(name, caption, ReportColumnTypes.WholeNumber);
    }

    private sealed record GroupFill(ReportGroup Group, int StudentsCount);
}
