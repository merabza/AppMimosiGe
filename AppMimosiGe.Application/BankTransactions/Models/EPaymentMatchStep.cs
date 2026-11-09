namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     რომელი წესით მოიძებნა ჩარიცხვის მოსწავლე(ები) (TBC_INTEGRATION.md §4); მენეჯერისთვის ნდობის ნიშანია
/// </summary>
public enum EPaymentMatchStep
{
    //ვერ მოიძებნა: ჩარიცხვა გაუნაწილებელი რჩება
    None = 0,

    //0. კონტრაქტის ნომერი დანიშნულებაში (D165)
    ContractNumber = 1,

    //1. მოსწავლის სახელი და გვარი დანიშნულებაში
    StudentName = 2,

    //1. მხოლოდ მოსწავლის გვარი დანიშნულებაში
    StudentSurname = 3,

    //2. გადამხდელის პირადი ნომერი (კონტრაქტის ან დასწავლილი გადამხდელის)
    PayerPersonalNumber = 4,

    //2. გადამხდელის ანგარიში (IBAN) დასწავლილ გადამხდელებში
    PayerAccount = 5,

    //2. გადამხდელის სახელი და გვარი (პირადი ნომრის გარეშე), ერთ კონტრაქტის გადამხდელზე
    PayerName = 6,

    //3. გადამხდელის გვარი = მოსწავლის გვარი
    PayerSurname = 7
}
