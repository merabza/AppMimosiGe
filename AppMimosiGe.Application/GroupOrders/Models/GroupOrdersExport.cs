using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 2-ის ფაილი (D195): ყველა ბრძანება ხაზებით, დაპაუზებულებიც, ისე, როგორც ბაზაში იყო წაშლამდე. ExportedAt და
///     ExportedByUserName: როდის და ვინ დააბრუნა ჯგუფების რედაქტირება; TransitionDate: გადასვლის დღე (D0), რომლიდანაც
///     ბრძანებები მოქმედებდა. პირადი მონაცემი (სახელები) ფაილში არ არის, მხოლოდ იდენტიფიკატორები
/// </summary>
public sealed record GroupOrdersExport(
    DateTime ExportedAt,
    string ExportedByUserName,
    DateTime? TransitionDate,
    IReadOnlyList<GroupOrderExport> Orders);
