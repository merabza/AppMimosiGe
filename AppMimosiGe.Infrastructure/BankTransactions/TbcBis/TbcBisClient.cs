using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

/// <summary>
///     თიბისის Business Integration Services - Accounts (D166): OAuth 2.0 client credentials ტოკენი (scope bab_accounts,
///     მხოლოდ ნახვა) და ანგარიშის მოძრაობები ფურცლებად (≤ 700). ტოკენი 1 საათი ძლებს და ინახება ვადის გასვლამდე 5 წუთით
///     ადრე; 401-ზე ინახული ტოკენი იშლება, რომ შემდეგმა გამოძახებამ ახალი აიღოს
/// </summary>
public sealed class TbcBisClient(HttpClient httpClient, TbcBisSettings settings, TimeProvider timeProvider)
    : IBankMovementsClient
{
    public const int PageSize = 700;

    //Transaction Types: 20 = Credit / income (1 = საკუთარ ანგარიშებს შორის, 5 = კონვერტაცია და სხვ. არ გვჭირდება)
    public const int IncomingTransactionType = 20;

    //Status Codes: 0 = Authorized (1, 2 = ავტორიზაციის მოლოდინში)
    public const int AuthorizedStatusCode = 0;

    private const string TokenPath = "oauth/token";
    private const string MovementsPath = "bab/v1/accounts/movements";
    private const string AccountsScope = "bab_accounts";
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private TbcAccessToken? _token;

    public IntegratedBankAccount? Account =>
        settings is { BankAccountId: { } bankAccountId, StartDate: { } startDate }
            ? new IntegratedBankAccount(bankAccountId, startDate)
            : null;

    public async Task<Result<IReadOnlyList<BankMovement>>> GetIncomingPayments(DateOnly periodFrom,
        DateOnly periodTo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.ClientId) ||
            string.IsNullOrWhiteSpace(settings.ClientSecret) || string.IsNullOrWhiteSpace(settings.AccountIban))
        {
            return Result.Failure<IReadOnlyList<BankMovement>>(BankTransactionErrors.BankIsNotConfigured);
        }

        List<BankMovement> movements = [];
        int fetched = 0;
        int pageIndex = 0;
        bool morePages;
        do
        {
            Result<string> token = await GetAccessToken(cancellationToken);
            if (token.IsFailure)
            {
                return Result.Failure<IReadOnlyList<BankMovement>>(token.Error);
            }

            Uri pageUri = MovementsUri(periodFrom, periodTo, pageIndex);
            Result<TbcMovementsResponse> page = await Send<TbcMovementsResponse>(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
                return request;
            }, cancellationToken);
            if (page.IsFailure)
            {
                return Result.Failure<IReadOnlyList<BankMovement>>(page.Error);
            }

            List<TbcMovement> transactions = page.Value.Transactions ?? [];
            fetched += transactions.Count;
            movements.AddRange(transactions.Where(IsIncomingPayment).Select(ToBankMovement));

            //ცარიელი ფურცელი ან ყველა წაკითხულია (total = ფილტრის შესაბამისი ჩანაწერების რაოდენობა)
            morePages = transactions.Count > 0 && fetched < page.Value.Total;
            pageIndex++;
        } while (morePages);

        return Result.Success<IReadOnlyList<BankMovement>>(movements);
    }

    //შემოსული (isDebit = false), ჩარიცხვა (transactionType 20), ავტორიზებული და იდენტიფიკატორით
    private static bool IsIncomingPayment(TbcMovement movement)
    {
        return !movement.IsDebit && movement.TransactionType == IncomingTransactionType &&
               movement.StatusCode == AuthorizedStatusCode && !string.IsNullOrWhiteSpace(movement.MovementId);
    }

    private static BankMovement ToBankMovement(TbcMovement movement)
    {
        TbcPartnerAccountInfo? partner = movement.PartnerAccountInfo;
        return new BankMovement(movement.MovementId!, movement.Date, movement.DocumentDate, movement.Amount,
            movement.Currency ?? string.Empty, movement.Purpose, movement.AdditionalInformation,
            movement.AdditionalDescription, movement.DocumentNumber, partner?.PartnerName, partner?.AccountIban,
            partner?.BankCode, partner?.TaxCode, partner?.PersonalNumber);
    }

    private async Task<Result<string>> GetAccessToken(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        TbcAccessToken? cached = _token;
        if (cached is not null && now < cached.ExpiresAt - TokenRefreshMargin)
        {
            return cached.Value;
        }

        string credentials =
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
        Result<TbcTokenResponse> response = await Send<TbcTokenResponse>(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ApiUri(TokenPath))
            {
                Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", AccountsScope)
                ])
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            return request;
        }, cancellationToken);
        if (response.IsFailure)
        {
            return Result.Failure<string>(response.Error);
        }

        if (string.IsNullOrWhiteSpace(response.Value.AccessToken) || response.Value.ExpiresIn <= 0)
        {
            return Result.Failure<string>(BankTransactionErrors.BankResponseIsInvalid);
        }

        _token = new TbcAccessToken(response.Value.AccessToken, now.AddSeconds(response.Value.ExpiresIn));
        return response.Value.AccessToken;
    }

    private async Task<Result<T>> Send<T>(Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = createRequest();
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _token = null;
                }

                return Result.Failure<T>(await ApiError(response, cancellationToken));
            }

            T? body = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return body is null
                ? Result.Failure<T>(BankTransactionErrors.BankResponseIsInvalid)
                : Result.Success<T>(body);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<T>(BankTransactionErrors.BankIsUnreachable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            //HttpClient-ის timeout
            return Result.Failure<T>(BankTransactionErrors.BankIsUnreachable);
        }
        catch (JsonException)
        {
            return Result.Failure<T>(BankTransactionErrors.BankResponseIsInvalid);
        }
    }

    //application/problem+json: კოდი და სათაური; სხვა ფორმის პასუხს მხოლოდ HTTP სტატუსი აქვს
    private static async Task<Error> ApiError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        TbcProblem? problem;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<TbcProblem>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            problem = null;
        }

        return BankTransactionErrors.BankApiError((int)response.StatusCode, problem?.Code, problem?.Title);
    }

    private Uri MovementsUri(DateOnly periodFrom, DateOnly periodTo, int pageIndex)
    {
        //ფილტრი მხოლოდ თარიღის ნაწილს იყენებს
        string query = string.Create(CultureInfo.InvariantCulture,
            $"AccountNumber={Uri.EscapeDataString(settings.AccountIban!)}&AccountCurrencyCode={Uri.EscapeDataString(settings.Currency)}&PeriodFrom={periodFrom:yyyy-MM-dd}T00:00:00&PeriodTo={periodTo:yyyy-MM-dd}T23:59:59&PageIndex={pageIndex}&PageSize={PageSize}");
        return ApiUri($"{MovementsPath}?{query}");
    }

    //BaseUrl ჰოსტის ფესვია (https://api.tbcbank.ge), ამიტომ ფარდობითი მისამართი მას ემატება
    private Uri ApiUri(string relative)
    {
        return new Uri(new Uri(settings.BaseUrl!), relative);
    }

    private sealed record TbcAccessToken(string Value, DateTimeOffset ExpiresAt);
}
