using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Payments;

public static class PaymentsRepositoryExtensions
{
    /// <summary>
    ///     გადახდა კონტრაქტის ბალანსს და შემდეგი გადახდის თარიღს ცვლის, ამიტომ კონტრაქტს DirtyNextPayDate ერთვება.
    ///     Access-ის FrmPayments ნებისმიერ ცვლილებაზე ყველა კონტრაქტის ალამს რთავდა (SetAllNextPayDateDirty); აქ მხოლოდ
    ///     გადახდის კონტრაქტისას, შეცვლისას ძველისა და ახლის (D78). ალამი იმავე SaveChanges-ით (ერთ ტრანზაქციაში) ინახება
    /// </summary>
    public static async Task MarkNextPayDatesDirty(this IPaymentsRepository repository,
        IEnumerable<int> studentContractIds, CancellationToken cancellationToken)
    {
        int[] ids = [.. studentContractIds.Distinct()];
        foreach (StudentContract studentContract in await repository.GetStudentContractsForChange(ids,
                     cancellationToken))
        {
            studentContract.DirtyNextPayDate = true;
        }
    }
}
