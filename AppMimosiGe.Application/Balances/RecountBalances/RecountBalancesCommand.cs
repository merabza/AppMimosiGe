using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Balances.RecountBalances;

//Access-ის FrmDeposites: გახსნისას (OnlyDirty) dirty ჯგუფების გაკვეთილები და dirty კონტრაქტების შემდეგი გადახდის
//თარიღი; "სრული გადაანგარიშება" (OnlyDirty = false) ყველა ჯგუფი და ყველა კონტრაქტი
public sealed record RecountBalancesCommand(bool OnlyDirty) : ICommand<BalancesRecountResponse>;
