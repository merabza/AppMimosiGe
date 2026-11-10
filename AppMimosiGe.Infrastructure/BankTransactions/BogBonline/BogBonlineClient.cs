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

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

/// <summary>
///     საქართველოს ბანკის Business Online API (D166): OAuth 2.0 client credentials ტოკენი (Keycloak) და ანგარიშის
///     ამონაწერი (Statement V2) ფურცლებად (≤ 1000; შემდეგი ფურცლები ამონაწერის id-ით, თანმიმდევრობით). მხოლოდ კითხულობს.
///     ტოკენი ახლდება ვადის გასვლამდე 5 წუთით ადრე (მოკლე ტოკენი ვადის ნახევარზე); 401-ზე იშლება. ამონაწერი 1-ჯერ წუთში შეიძლება (429 → Retry-After)
/// </summary>
public sealed class BogBonlineClient(HttpClient httpClient, BogBonlineSettings settings, TimeProvider timeProvider)
    : IBankMovementsClient
{
    private const string StatementPath = "v2/statement";
    private static readonly TimeSpan MaxTokenRefreshMargin = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private BogAccessToken? _token;

    public IntegratedBankAccount? Account =>
        settings is { BankAccountId: { } bankAccountId, StartDate: { } startDate }
            ? new IntegratedBankAccount(bankAccountId, startDate)
            : null;

    public async Task<Result<IReadOnlyList<BankMovement>>> GetIncomingPayments(DateOnly periodFrom,
        DateOnly periodTo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.TokenUrl) || string.IsNullOrWhiteSpace(settings.BaseUrl) ||
            string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret) ||
            string.IsNullOrWhiteSpace(settings.AccountIban))
        {
            return Result.Failure<IReadOnlyList<BankMovement>>(BankTransactionErrors.BankIsNotConfigured);
        }

        List<BankMovement> movements = [];
        int fetched = 0;
        long? statementId = null;
        int page = 1;
        bool morePages;
        do
        {
            Result<string> token = await GetAccessToken(cancellationToken);
            if (token.IsFailure)
            {
                return Result.Failure<IReadOnlyList<BankMovement>>(token.Error);
            }

            Uri pageUri = statementId is { } id ? NextPageUri(id, page) : StatementUri(periodFrom, periodTo);
            Result<BogStatementResponse> statement = await Send<BogStatementResponse>(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
                return request;
            }, cancellationToken);
            if (statement.IsFailure)
            {
                return Result.Failure<IReadOnlyList<BankMovement>>(statement.Error);
            }

            List<BogStatementRecord> records = statement.Value.Records ?? [];
            fetched += records.Count;
            movements.AddRange(records.Where(IsIncomingPayment).Select(ToBankMovement));

            //ცარიელი ფურცელი ან ყველა წაკითხულია (TotalCount = პერიოდის ყველა ჩანაწერი)
            statementId = statement.Value.Id;
            morePages = records.Count > 0 && fetched < statement.Value.TotalCount;
            page++;
        } while (morePages);

        return Result.Success<IReadOnlyList<BankMovement>>(movements);
    }

    //შემოსული (კრედიტი, დებეტის გარეშე), იდენტიფიკატორით და არა ორგანიზაციის საკუთარი ანგარიშიდან (გამგზავნის
    //საიდენტიფიკაციო კოდი = მიმღებისა)
    private static bool IsIncomingPayment(BogStatementRecord record)
    {
        return record is { EntryId: not null, EntryAmountCredit: > 0, EntryDate: not null } &&
               (record.EntryAmountDebit ?? 0) == 0 && !IsOwnTransfer(record);
    }

    private static bool IsOwnTransfer(BogStatementRecord record)
    {
        string? senderInn = record.SenderDetails?.Inn?.Trim();
        return !string.IsNullOrEmpty(senderInn) &&
               string.Equals(senderInn, record.BeneficiaryDetails?.Inn?.Trim(), StringComparison.Ordinal);
    }

    private BankMovement ToBankMovement(BogStatementRecord record)
    {
        BogPartyDetails? sender = record.SenderDetails;
        //EntryId რიცხვია (JSON-ში "123.0"): ბოლო ნულების გარეშე, რომ ერთი ჩანაწერი ყოველთვის ერთნაირად ჩაიწეროს
        return new BankMovement(record.EntryId!.Value.ToString("G29", CultureInfo.InvariantCulture),
            record.EntryDate!.Value.DateTime, record.DocumentValueDate?.DateTime, record.EntryAmountCredit!.Value,
            settings.Currency, record.DocumentNomination, record.DocumentInformation, null,
            record.EntryDocumentNumber, sender?.Name, sender?.AccountNumber, sender?.BankCode, sender?.Inn,
            record.DocumentPayerInn);
    }

    private async Task<Result<string>> GetAccessToken(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        BogAccessToken? cached = _token;
        if (cached is not null && now < cached.RefreshAt)
        {
            return cached.Value;
        }

        string credentials =
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
        Result<BogTokenResponse> response = await Send<BogTokenResponse>(() =>
        {
            //დოკუმენტაციით: body-ში grant_type, client_id და client_secret, და Basic სათაურიც
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(settings.TokenUrl!))
            {
                Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", settings.ClientId!),
                    new KeyValuePair<string, string>("client_secret", settings.ClientSecret!)
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

        //Keycloak-ის ტოკენი შეიძლება მხოლოდ 5 წუთი ძლებდეს: მაშინ ნახევარი ვადით ადრე ახლდება
        TimeSpan lifetime = TimeSpan.FromSeconds(response.Value.ExpiresIn);
        TimeSpan margin = lifetime / 2 < MaxTokenRefreshMargin ? lifetime / 2 : MaxTokenRefreshMargin;
        _token = new BogAccessToken(response.Value.AccessToken, now + lifetime - margin);
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

                return Result.Failure<T>(response.StatusCode == HttpStatusCode.TooManyRequests
                    ? BankTransactionErrors.BankRateLimitExceeded(RetryAfterSeconds(response))
                    : await ApiError(response, cancellationToken));
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

    private int? RetryAfterSeconds(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        TimeSpan? wait = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - timeProvider.GetUtcNow() : null);
        return wait is { } value ? Math.Max(1, (int)Math.Ceiling(value.TotalSeconds)) : null;
    }

    //Keycloak: error + error_description; API: Message. სხვა ფორმის პასუხს მხოლოდ HTTP სტატუსი აქვს
    private static async Task<Error> ApiError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        BogError? error;
        try
        {
            error = await response.Content.ReadFromJsonAsync<BogError>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            error = null;
        }

        return BankTransactionErrors.BankApiError((int)response.StatusCode, error?.Error,
            error?.ErrorDescription ?? error?.Message);
    }

    //ანგარიში და ვალუტა ცალ-ცალკე, თარიღები yyyy-MM-dd (ორივე ჩათვლით). /true/false: includeToday (D175: ძირითადი
    //ამონაწერი დღევანდელ ჩანაწერებს არ აჩვენებს) და orderByDate = false
    private Uri StatementUri(DateOnly periodFrom, DateOnly periodTo)
    {
        return ApiUri(string.Create(CultureInfo.InvariantCulture,
            $"{StatementPath}/{AccountSegments()}/{periodFrom:yyyy-MM-dd}/{periodTo:yyyy-MM-dd}/true/false"));
    }

    private Uri NextPageUri(long statementId, int page)
    {
        return ApiUri(string.Create(CultureInfo.InvariantCulture,
            $"{StatementPath}/{AccountSegments()}/{statementId}/{page}"));
    }

    private string AccountSegments()
    {
        return $"{Uri.EscapeDataString(settings.AccountIban!.Trim())}/{Uri.EscapeDataString(settings.Currency)}";
    }

    //BaseUrl-ს ბოლო "/"-ით ან მის გარეშე: ფარდობითი მისამართი "api"-ს შემდეგ ემატება
    private Uri ApiUri(string relative)
    {
        return new Uri($"{settings.BaseUrl!.TrimEnd('/')}/{relative}");
    }

    private sealed record BogAccessToken(string Value, DateTimeOffset RefreshAt);
}
