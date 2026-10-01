using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გაკვეთილების გენერატორის სუფთა ლოგიკა Access-ის GroupData-ს, LessonData-ს და LessonStudentData-ს მიხედვით.
///     ჯგუფის მასწავლებლებიდან, მოსწავლეებიდან, განრიგიდან და არსებული გაკვეთილებიდან ითვლის გეგმას: რომელი გაკვეთილი
///     შეიქმნას, შეიცვალოს ან წაიშალოს, რა ჩაიწეროს გენერატორის ლოგში და რომელ კონტრაქტებს დაენთოს DirtyNextPayDate.
///     ბაზას არ მიმართავს. Access-ისგან განსხვავებები (D61–D64): დღეები მხოლოდ თარიღით ითვლება, ჰორიზონტის ბოლო დღე
///     VoidDate-ის მიუხედავად ერთნაირად ითვლება, შეცდომები 1–3 ერთხელ იწერება, ერთი კონტრაქტის ორი მოქმედი სტრიქონიდან
///     პირველი გამოიყენება
/// </summary>
public static class GroupLessonsPlanner
{
    /// <summary>
    ///     Access-ის CheckLessons: ჯგუფის ყოველი დღე StartDate-იდან EndDate-მდე. horizonEnd: ბოლო სამუშაო თვის ბოლო დღე
    /// </summary>
    public static GroupLessonsPlan PlanGroup(GroupLessonsInput input, DateTime horizonEnd)
    {
        var builder = new PlanBuilder(input, horizonEnd);
        if (!builder.IsValid)
        {
            return builder.Build(false, null);
        }

        for (DateTime day = builder.StartDate; day <= builder.EndDate; day = day.AddDays(1))
        {
            builder.CheckLessonForDate(day);
        }

        return builder.Build(true, null);
    }

    /// <summary>
    ///     Access-ის CheckLastLesson: დღიდან უკან StartDate-მდე პირველი დღე, როცა გაკვეთილი უნდა იყოს. ის და გზად გავლილი
    ///     დღეები ისევე სწორდება, როგორც სრულ გენერაციაში. ჯგუფის DirtyLessons ამ დროს არ ქრება
    /// </summary>
    public static GroupLessonsPlan PlanLastLesson(GroupLessonsInput input, DateTime horizonEnd, DateTime today)
    {
        var builder = new PlanBuilder(input, horizonEnd);
        if (!builder.IsValid)
        {
            return builder.Build(false, null);
        }

        for (DateTime day = today.Date; day >= builder.StartDate; day = day.AddDays(-1))
        {
            PlannedLastLesson? lastLesson = builder.CheckLessonForDate(day);
            if (lastLesson is not null)
            {
                return builder.Build(false, lastLesson);
            }
        }

        return builder.Build(false, null);
    }

    private sealed record MustStudent(int GbsId, int StudentContractId, float HoursCount);

    //Access-ის CountMustLesson-ის შედეგი: როგორი უნდა იყოს გაკვეთილი ამ დღეს
    private sealed record MustLesson(LessonValues Values, IReadOnlyList<MustStudent> Students);

    private sealed class PlanBuilder
    {
        private readonly List<PlannedLessonChange> _changes = [];
        private readonly HashSet<int> _dirtyStudentContractIds = [];
        private readonly GroupLessonsInput _input;
        private readonly Dictionary<DateTime, List<ExistingLesson>> _lessonsByDay;
        private readonly List<PlannedLogEntry> _logs = [];
        private readonly DateTime _minValidDate;

        //Access-ის ჩატვირთვის რიგი: მოსწავლეები StudentContractId-ით
        private readonly List<GeneratorStudentRow> _students;

        //გაკვეთილი შეიქმნა, შეიცვალა ან წაიშალა: ჯგუფის ყველა მოსწავლის კონტრაქტი ინიშნება
        //(Access-ის SetStudentNextPayDateDirtyForGroup)
        private bool _groupLessonsChanged;

        public PlanBuilder(GroupLessonsInput input, DateTime horizonEnd)
        {
            _input = input;
            _students = [.. input.Students.OrderBy(s => s.StudentContractId).ThenBy(s => s.GbsId)];
            _lessonsByDay = input.Lessons.GroupBy(l => l.Values.LessonDt.Date).ToDictionary(g => g.Key,
                g => g.OrderBy(l => l.Values.LessonDt).ThenBy(l => l.Id).ToList());

            IsValid = Validate();
            if (!IsValid)
            {
                return;
            }

            StartDate = CountStartDate(input);

            //EndDate ბოლო სამუშაო თვის ბოლო დღეა, ან უფრო გვიანდელი არსებული გაკვეთილის დღე (ის უნდა შემოწმდეს)
            DateTime dayAfterEndDate = horizonEnd.Date.AddDays(1);
            EndDate = horizonEnd.Date;
            if (input.Lessons.Count > 0)
            {
                DateTime lastLessonDay = input.Lessons.Max(l => l.Values.LessonDt).Date;
                if (lastLessonDay > EndDate)
                {
                    EndDate = lastLessonDay;
                }
            }

            //გაკვეთილის დრო ამაზე გვიან ვერ იქნება: ჯგუფის გაუქმება, ჰორიზონტის შემდეგი დღე და მასწავლებლების,
            //მოსწავლეებისა და განრიგის ყველაზე გვიანდელი დასრულებები. Access VoidDate-ის შემთხვევაში ჰორიზონტის შემდეგი
            //დღის ნაცვლად ჰორიზონტის ბოლო დღეს იღებდა და იმ დღის გაკვეთილს აღარ ქმნიდა (D62)
            _minValidDate = Min(input.VoidDate ?? dayAfterEndDate, dayAfterEndDate);
            _minValidDate = Min(_minValidDate, MaxEndDate(input.Teachers.Select(t => t.EndDate), dayAfterEndDate));
            _minValidDate = Min(_minValidDate, MaxEndDate(input.Students.Select(s => s.EndDate), dayAfterEndDate));
            _minValidDate = Min(_minValidDate, MaxEndDate(input.DayTimePlaces.Select(d => d.EndDate), dayAfterEndDate));
        }

        public bool IsValid { get; }
        public DateTime StartDate { get; }
        public DateTime EndDate { get; }

        //Access-ის IsValid: მასწავლებლის, მოსწავლის ან განრიგის გარეშე გაკვეთილები არ ითვლება და არსებული არ იცვლება.
        //Access ამ შეცდომას ორჯერ წერდა (Init-ში და CheckLessons-ში), აქ ერთხელ იწერება (D63)
        private bool Validate()
        {
            int errorCode;
            if (_input.Teachers.Count == 0)
            {
                errorCode = LessonGeneratorErrorCodes.NoTeachers;
            }
            else if (_input.Students.Count == 0)
            {
                errorCode = LessonGeneratorErrorCodes.NoStudents;
            }
            else if (_input.DayTimePlaces.Count == 0)
            {
                errorCode = LessonGeneratorErrorCodes.NoDayTimePlaces;
            }
            else
            {
                return true;
            }

            AddLog(errorCode, null, null);
            return false;
        }

        //StartDate = MAX(მასწავლებლების, მოსწავლეების და განრიგის ყველაზე ადრეული დაწყება), ან უფრო ადრეული არსებული
        //გაკვეთილის დღე. მხოლოდ თარიღი: Access-ში აქ გაკვეთილის დრო რჩებოდა და ციკლის დღეებს ყველას ემატებოდა (D61)
        private static DateTime CountStartDate(GroupLessonsInput input)
        {
            DateTime[] firstStartDates =
            [
                input.Teachers.Min(t => t.StartDate), input.Students.Min(s => s.StartDate),
                input.DayTimePlaces.Min(d => d.StartDate)
            ];
            DateTime startDate = firstStartDates.Max().Date;
            if (input.Lessons.Count > 0)
            {
                DateTime firstLessonDay = input.Lessons.Min(l => l.Values.LessonDt).Date;
                if (firstLessonDay < startDate)
                {
                    startDate = firstLessonDay;
                }
            }

            return startDate;
        }

        //Access-ის CheckLessonForDate: ადარებს, როგორი უნდა იყოს გაკვეთილი ამ დღეს და როგორია. აბრუნებს გაკვეთილს, თუ ამ
        //დღეს ის უნდა იყოს
        public PlannedLastLesson? CheckLessonForDate(DateTime day)
        {
            MustLesson? mustLesson = CountMustLesson(day);
            ExistingLesson? existingLesson = FindExistingLesson(day, mustLesson?.Values.LessonDt);
            if (mustLesson is null)
            {
                if (existingLesson is not null)
                {
                    RemoveExtraLesson(existingLesson);
                }

                return null;
            }

            if (existingLesson is null)
            {
                CreateLesson(mustLesson);
                return new PlannedLastLesson(null, mustLesson.Values.LessonDt);
            }

            UpdateLesson(existingLesson, mustLesson);
            return new PlannedLastLesson(existingLesson.Id, mustLesson.Values.LessonDt);
        }

        //Access ეძებს გაკვეთილს დღით (წელი, თვე, რიცხვი) და არა ზუსტი დროით. ერთ დღეს რამდენიმე გაკვეთილს გენერატორი
        //არ ქმნის; თუ მაინც არის, ირჩევა საჭირო დროისა, თუ არა, ყველაზე ადრეული. დანარჩენებს გენერატორი არ ეხება
        private ExistingLesson? FindExistingLesson(DateTime day, DateTime? mustLessonDt)
        {
            if (!_lessonsByDay.TryGetValue(day, out List<ExistingLesson>? lessons))
            {
                return null;
            }

            return lessons.Find(l => l.Values.LessonDt == mustLessonDt) ?? lessons[0];
        }

        //Access-ის CountMustLesson. null: ამ დღეს გაკვეთილი არ უნდა იყოს
        private MustLesson? CountMustLesson(DateTime day)
        {
            List<GeneratorDayTimePlaceRow> dayTimePlaces = [.. _input.DayTimePlaces.Where(d => IsLessonDay(d, day))];
            if (dayTimePlaces.Count == 0)
            {
                return null;
            }

            if (dayTimePlaces.Count > 1)
            {
                AddLog(LessonGeneratorErrorCodes.TwoDayTimePlacesOnDay, day, null);
            }

            GeneratorDayTimePlaceRow dayTimePlace = dayTimePlaces[0];
            DateTime lessonDt = day.Add(dayTimePlace.StartTime.ToTimeSpan());
            if (_minValidDate < lessonDt)
            {
                return null;
            }

            //მოსწავლის გარეშე დღეს გაკვეთილი არ არის; შეცდომა 4 Access-შიც დაკომენტარებული იყო. ერთი კონტრაქტის ორი
            //მოქმედი სტრიქონიდან მხოლოდ პირველი (უმცირესი GbsId) ითვლება (D64)
            List<GeneratorStudentRow> students =
                [.. _students.Where(s => IsActive(s.StartDate, s.EndDate, day)).DistinctBy(s => s.StudentContractId)];
            if (students.Count == 0)
            {
                return null;
            }

            List<GeneratorTeacherRow> teachers = [.. _input.Teachers.Where(t => IsActive(t.StartDate, t.EndDate, day))];
            if (teachers.Count > 1)
            {
                AddLog(LessonGeneratorErrorCodes.TwoTeachersOnDay, day, null);
            }

            if (teachers.Count == 0)
            {
                AddLog(LessonGeneratorErrorCodes.NoTeacherOnDay, day, null);
                return null;
            }

            (DateTime teoMinDate, DateTime teoMaxDate) = CountTeoDates(day);
            var values = new LessonValues(lessonDt, teachers[0].TeacherContractId, teachers[0].SalarySchemaId,
                students.Max(s => s.FourWeekHours), teoMinDate, teoMaxDate);
            return new MustLesson(values, [
                .. students.Select(s =>
                    new MustStudent(s.GbsId, s.StudentContractId, s.HoursCoefficient * dayTimePlace.HoursCount))
            ]);
        }

        //დღის თვეში თეორიულად პირველი და ბოლო გაკვეთილის დრო: ყოველი განრიგისთვის, რომელიც ამ დღეს მოქმედებს
        //(კვირის დღის გარეშე), თვის პირველი და ბოლო დღე, როცა ის ზუსტად მოქმედებს. საწყისი მნიშვნელობები
        //შებრუნებულია (Access-ის „ukugma"): TeoMin თვის ბოლო წამია, TeoMax თვის დასაწყისი
        private (DateTime TeoMinDate, DateTime TeoMaxDate) CountTeoDates(DateTime day)
        {
            DateTime monthStart = day.AddDays(1 - day.Day);
            DateTime nextMonthStart = monthStart.AddMonths(1);
            DateTime teoMinDate = nextMonthStart.AddSeconds(-1);
            DateTime teoMaxDate = monthStart;
            foreach (GeneratorDayTimePlaceRow dayTimePlace in _input.DayTimePlaces.Where(d =>
                         IsActive(d.StartDate, d.EndDate, day)))
            {
                if (FirstLessonDateTime(dayTimePlace, monthStart, nextMonthStart) is { } first && first < teoMinDate)
                {
                    teoMinDate = first;
                }

                if (LastLessonDateTime(dayTimePlace, monthStart, nextMonthStart) is { } last && last > teoMaxDate)
                {
                    teoMaxDate = last;
                }
            }

            return (teoMinDate, teoMaxDate);
        }

        //ზედმეტი გაკვეთილი: შეტანილი მონაცემით რჩება (შეცდომა 11), სხვა შემთხვევაში მოსწავლეების სტრიქონებიანად იშლება.
        //შეტანილი მონაცემი მხოლოდ მოსწავლეების სტრიქონებზე მოწმდება, როგორც Access-ში (D65)
        private void RemoveExtraLesson(ExistingLesson lesson)
        {
            if (lesson.Students.Any(s => s.HasEnteredData))
            {
                AddLog(LessonGeneratorErrorCodes.ExtraLessonHasEnteredData, lesson.Values.LessonDt, lesson.Id);
                return;
            }

            _changes.Add(new PlannedLessonChange(ELessonChangeKind.Delete, lesson.Id, lesson.Values, null, [
                .. lesson.Students.Select(s => new PlannedStudentRow(EStudentRowChangeKind.Delete, s.Id,
                    s.StudentContractId, s.GroupByStudentId, s.HoursCount))
            ]));
            _groupLessonsChanged = true;
            //სტრიქონის კონტრაქტი შეიძლება ჯგუფში აღარ იყოს (Access მას არ ნიშნავდა)
            _dirtyStudentContractIds.UnionWith(lesson.Students.Select(s => s.StudentContractId));
        }

        private void CreateLesson(MustLesson mustLesson)
        {
            _changes.Add(new PlannedLessonChange(ELessonChangeKind.Create, null, mustLesson.Values, null, [
                .. mustLesson.Students.Select(s => new PlannedStudentRow(EStudentRowChangeKind.Add, null,
                    s.StudentContractId, s.GbsId, s.HoursCount))
            ]));
            _groupLessonsChanged = true;
        }

        //გაკვეთილის ველები სწორდება მაშინაც, როცა შეტანილი მონაცემი აქვს (შეცდომა 8 Access-შიც არ იწერებოდა)
        private void UpdateLesson(ExistingLesson lesson, MustLesson mustLesson)
        {
            LessonValues? previousValues = lesson.Values == mustLesson.Values ? null : lesson.Values;
            List<PlannedStudentRow> studentRows = MergeStudents(lesson, mustLesson);
            if (previousValues is null && studentRows.Count == 0)
            {
                return;
            }

            if (previousValues is not null)
            {
                _groupLessonsChanged = true;
            }

            _dirtyStudentContractIds.UnionWith(studentRows.Select(r => r.StudentContractId));
            _changes.Add(new PlannedLessonChange(ELessonChangeKind.Update, lesson.Id, mustLesson.Values, previousValues,
                studentRows));
        }

        //Access-ის UpdateStudents: ორივე სია StudentContractId-ით დალაგებულია და ერთად გაივლება. ერთნაირი კონტრაქტის
        //სტრიქონი სწორდება (შეტანილი მონაცემის შემოწმება, შეცდომა 13, Access-შიც დაკომენტარებული იყო), ახალი ემატება,
        //ზედმეტი იშლება ან, შეტანილი მონაცემით, რჩება (შეცდომა 14)
        private List<PlannedStudentRow> MergeStudents(ExistingLesson lesson, MustLesson mustLesson)
        {
            List<ExistingLessonStudent> current =
                [.. lesson.Students.OrderBy(s => s.StudentContractId).ThenBy(s => s.Id)];
            IReadOnlyList<MustStudent> must = mustLesson.Students;
            List<PlannedStudentRow> rows = [];
            int mustIndex = 0;
            int currentIndex = 0;
            while (mustIndex < must.Count || currentIndex < current.Count)
            {
                bool hasMust = mustIndex < must.Count;
                bool hasCurrent = currentIndex < current.Count;
                if (hasMust && hasCurrent &&
                    must[mustIndex].StudentContractId == current[currentIndex].StudentContractId)
                {
                    MustStudent mustStudent = must[mustIndex];
                    ExistingLessonStudent currentStudent = current[currentIndex];
                    if (!currentStudent.HoursCount.Equals(mustStudent.HoursCount) ||
                        currentStudent.GroupByStudentId != mustStudent.GbsId)
                    {
                        rows.Add(new PlannedStudentRow(EStudentRowChangeKind.Update, currentStudent.Id,
                            currentStudent.StudentContractId, mustStudent.GbsId, mustStudent.HoursCount));
                    }

                    mustIndex++;
                    currentIndex++;
                }
                else if (hasMust && (!hasCurrent ||
                                     must[mustIndex].StudentContractId < current[currentIndex].StudentContractId))
                {
                    MustStudent mustStudent = must[mustIndex];
                    rows.Add(new PlannedStudentRow(EStudentRowChangeKind.Add, null, mustStudent.StudentContractId,
                        mustStudent.GbsId, mustStudent.HoursCount));
                    mustIndex++;
                }
                else
                {
                    ExistingLessonStudent currentStudent = current[currentIndex];
                    if (currentStudent.HasEnteredData)
                    {
                        AddLog(LessonGeneratorErrorCodes.ExtraStudentHasEnteredData, mustLesson.Values.LessonDt,
                            lesson.Id);
                    }
                    else
                    {
                        rows.Add(new PlannedStudentRow(EStudentRowChangeKind.Delete, currentStudent.Id,
                            currentStudent.StudentContractId, currentStudent.GroupByStudentId,
                            currentStudent.HoursCount));
                    }

                    currentIndex++;
                }
            }

            return rows;
        }

        private void AddLog(int errorCode, DateTime? lessonDate, int? lessonId)
        {
            _logs.Add(new PlannedLogEntry(errorCode, lessonDate, lessonId));
        }

        public GroupLessonsPlan Build(bool clearDirtyLessons, PlannedLastLesson? lastLesson)
        {
            IEnumerable<int> dirtyStudentContractIds = _groupLessonsChanged
                ? _dirtyStudentContractIds.Concat(_input.Students.Select(s => s.StudentContractId))
                : _dirtyStudentContractIds;
            return new GroupLessonsPlan(_changes, _logs, [.. dirtyStudentContractIds.Distinct().Order()],
                clearDirtyLessons, lastLesson);
        }

        //Access-ის IsValid: პერიოდი [StartDate, EndDate), EndDate null დაუსრულებელ პერიოდს ნიშნავს
        private static bool IsActive(DateTime startDate, DateTime? endDate, DateTime day)
        {
            return startDate <= day && (endDate is null || endDate > day);
        }

        //Access-ის WeekDay(date, vbMonday): 1 = ორშაბათი ... 7 = კვირა, როგორც WeekDays-ის ID-ები
        private static int WeekDayNumber(DateTime day)
        {
            return ((int)day.DayOfWeek + 6) % 7 + 1;
        }

        //Access-ის GroupDayTimeData.IsValid(date, Exact:=True): კვირის დღე ემთხვევა და განრიგი ამ დღეს მოქმედებს
        private static bool IsLessonDay(GeneratorDayTimePlaceRow dayTimePlace, DateTime day)
        {
            return dayTimePlace.WeekDayId == WeekDayNumber(day) &&
                   IsActive(dayTimePlace.StartDate, dayTimePlace.EndDate, day);
        }

        //GetEndDateNotMoreThen: დასრულება, მაგრამ არა ჰორიზონტის შემდეგი დღის შემდეგ; დაუსრულებელი = ჰორიზონტის შემდეგი დღე
        private static DateTime MaxEndDate(IEnumerable<DateTime?> endDates, DateTime dayAfterEndDate)
        {
            return endDates.Max(endDate =>
                endDate is null || endDate > dayAfterEndDate ? dayAfterEndDate : endDate.Value);
        }

        private static DateTime Min(DateTime first, DateTime second)
        {
            return first < second ? first : second;
        }

        //Access-ის GetFirstValidDateTime: თვის პირველი დღე, როცა განრიგი ზუსტად მოქმედებს, + დაწყების დრო
        private static DateTime? FirstLessonDateTime(GeneratorDayTimePlaceRow dayTimePlace, DateTime monthStart,
            DateTime nextMonthStart)
        {
            for (DateTime day = monthStart; day < nextMonthStart; day = day.AddDays(1))
            {
                if (IsLessonDay(dayTimePlace, day))
                {
                    return day.Add(dayTimePlace.StartTime.ToTimeSpan());
                }
            }

            return null;
        }

        //Access-ის GetLastValidDateTime: თვის ბოლო დღე, როცა განრიგი ზუსტად მოქმედებს, + დაწყების დრო
        private static DateTime? LastLessonDateTime(GeneratorDayTimePlaceRow dayTimePlace, DateTime monthStart,
            DateTime nextMonthStart)
        {
            for (DateTime day = nextMonthStart.AddDays(-1); day >= monthStart; day = day.AddDays(-1))
            {
                if (IsLessonDay(dayTimePlace, day))
                {
                    return day.Add(dayTimePlace.StartTime.ToTimeSpan());
                }
            }

            return null;
        }
    }
}
