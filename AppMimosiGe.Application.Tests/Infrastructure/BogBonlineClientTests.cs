using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Infrastructure.BankTransactions.BogBonline;
using AppMimosiGeShared.Contracts.Errors;
using Moq;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class BogBonlineClientTests : IDisposable
{
    private const string TokenJson =
        """{"access_token":"tok-1","expires_in":300,"refresh_expires_in":0,"token_type":"Bearer","not-before-policy":0,"scope":"corp"}""";

    private const string Iban = "GE29BG0000000123456789";
    private const string OurInn = "204567890";
    private static readonly DateOnly From = new(2026, 9, 1);
    private static readonly DateOnly To = new(2026, 9, 30);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly Mock<TimeProvider> _timeProvider = new();

    private const string TokenUrl = "https://account.bog.ge/auth/realms/bog/protocol/openid-connect/token";
    private const string BaseUrl = "https://api.businessonline.ge/api";

    private readonly BogBonlineSettings _settings = new()
    {
        TokenUrl = TokenUrl,
        BaseUrl = BaseUrl,
        ClientId = "id",
        ClientSecret = "secret",
        AccountIban = Iban
    };

    public BogBonlineClientTests()
    {
        _httpClient = new HttpClient(_handler);
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    private BogBonlineClient CreateClient()
    {
        return new BogBonlineClient(_httpClient, _settings, _timeProvider.Object);
    }

    // the integrated account needs both the account record and the start date
    [Fact]
    public void Account_IsSetOnlyWithTheAccountAndTheStartDate()
    {
        DateTime startDate = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Null(CreateClient().Account);
        _settings.BankAccountId = 1;
        Assert.Null(CreateClient().Account);
        _settings.StartDate = startDate;
        Assert.Equal(new IntegratedBankAccount(1, startDate), CreateClient().Account);
        _settings.BankAccountId = null;
        Assert.Null(CreateClient().Account);
    }

    private static string Entry(string entryId, decimal credit = 150.5m, decimal debit = 0m,
        string senderInn = "01001054321")
    {
        string amount = credit > 0 ? credit.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0.0";
        string debitText = debit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $$"""
                 {"entryDate":"2026-09-15T16:26:53.4094325+04:00","entryDocumentNumber":"DOC-1",
                  "entryAccountNumber":"{{Iban}}GEL","entryAmountDebit":{{debitText}},"entryAmountDebitBase":0.0,
                  "entryAmountCredit":{{amount}},"entryAmountCreditBase":{{amount}},"entryAmountBase":{{amount}},
                  "entryAmount":{{amount}},"entryComment":"comment","documentProductGroup":"PMD",
                  "documentValueDate":"2026-09-14T00:00:00+04:00",
                  "senderDetails":{"name":"Partner Name","inn":"{{senderInn}}","accountNumber":"GE61TB9876543210987654",
                   "bankCode":"TBCBGE22","bankName":"TBC Bank"},
                  "beneficiaryDetails":{"name":"Mimosi","inn":"{{OurInn}}","accountNumber":"{{Iban}}",
                   "bankCode":"BAGAGE22","bankName":"Bank of Georgia"},
                  "documentTreasuryCode":null,"documentNomination":"სწავლის საფასური","documentInformation":"info",
                  "documentSourceAmount":{{amount}},"documentSourceCurrency":"GEL","documentDestinationAmount":{{amount}},
                  "documentDestinationCurrency":"GEL","documentReceiveDate":"2026-09-15T16:26:53+04:00",
                  "documentRate":1.0,"documentKey":555.0,"entryId":{{entryId}},"documentPayerName":null,
                  "documentPayerInn":"01001012345","authDate":"2026-09-15T16:26:53+04:00","docComment":"doc comment"}
                 """;
    }

    private static string Statement(long id, int totalCount, params string[] entries)
    {
        return $$"""{"id":{{id}},"count":{{entries.Length}},"totalCount":{{totalCount}},"records":[{{string.Join(",", entries)}}]}""";
    }

    [Fact]
    public async Task GetIncomingPayments_RequestsATokenWithClientCredentialsAndTheStatementWithIt()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(77, 1, Entry("1001.0")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, _handler.Requests.Count);
        RecordedRequest token = _handler.Requests[0];
        Assert.Equal(HttpMethod.Post, token.Method);
        Assert.Equal("https://account.bog.ge/auth/realms/bog/protocol/openid-connect/token", token.Uri);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("id:secret"))}", token.Authorization);
        Assert.Equal("grant_type=client_credentials&client_id=id&client_secret=secret", token.Body);
        RecordedRequest statement = _handler.Requests[1];
        Assert.Equal(HttpMethod.Get, statement.Method);
        Assert.Equal($"https://api.businessonline.ge/api/v2/statement/{Iban}/GEL/2026-09-01/2026-09-30/true/false",
            statement.Uri);
        Assert.Equal("Bearer tok-1", statement.Authorization);
    }

    // a base URL with the trailing slash too
    [Fact]
    public async Task GetIncomingPayments_BaseUrlWithTrailingSlash_KeepsItsPath()
    {
        // Arrange
        _settings.BaseUrl = "https://test.businessonline.ge/api/";
        _settings.AccountIban = $" {Iban} ";
        _settings.Currency = "USD";
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(1, 0));

        // Act
        await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal($"https://test.businessonline.ge/api/v2/statement/{Iban}/USD/2026-09-01/2026-09-30/true/false",
            _handler.Requests[1].Uri);
    }

    [Fact]
    public async Task GetIncomingPayments_MapsTheEntryFields()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(77, 1, Entry("1001.0")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert: the bank's wall-clock time (+04:00) and the entry id without the trailing zeros
        Assert.Equal(
            new BankMovement("1001", new DateTime(2026, 9, 15, 16, 26, 53, DateTimeKind.Unspecified).AddTicks(4094325),
                new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Unspecified), 150.5m, "GEL", "სწავლის საფასური",
                "info", null, "DOC-1", "Partner Name", "GE61TB9876543210987654", "TBCBGE22", "01001054321",
                "01001012345"), Assert.Single(result.Value));
    }

    [Fact]
    public async Task GetIncomingPayments_KeepsOnlyIncomingCreditsFromOthers()
    {
        // Arrange: outgoing, zero, own-account transfer, no id, then the one incoming credit
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK,
            Statement(77, 5, Entry("1", 0m, 20m), Entry("2", 0m), Entry("3", senderInn: $" {OurInn}"),
                Entry("null"), Entry("5")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(["5"], result.Value.Select(m => m.MovementId));
    }

    // a sender without an identification code is never taken for our own account
    [Fact]
    public async Task GetIncomingPayments_SenderWithoutInn_IsKept()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(77, 1, Entry("5", senderInn: " ")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(["5"], result.Value.Select(m => m.MovementId));
    }

    [Fact]
    public async Task GetIncomingPayments_ReadsTheNextPagesByTheStatementIdInOrder()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(77, 4, Entry("1"), Entry("2")));
        _handler.Respond(HttpStatusCode.OK, Statement(77, 4, Entry("3")));
        _handler.Respond(HttpStatusCode.OK, Statement(77, 4, Entry("4")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert: one token for every page
        Assert.Equal(["1", "2", "3", "4"], result.Value.Select(m => m.MovementId));
        Assert.Equal(4, _handler.Requests.Count);
        Assert.Equal($"https://api.businessonline.ge/api/v2/statement/{Iban}/GEL/77/2", _handler.Requests[2].Uri);
        Assert.Equal($"https://api.businessonline.ge/api/v2/statement/{Iban}/GEL/77/3", _handler.Requests[3].Uri);
        Assert.Equal("Bearer tok-1", _handler.Requests[3].Authorization);
    }

    [Fact]
    public async Task GetIncomingPayments_StopsAtAnEmptyPage()
    {
        // Arrange: the total promises more, but the page is empty
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(77, 10, Entry("1")));
        _handler.Respond(HttpStatusCode.OK, Statement(77, 10));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(["1"], result.Value.Select(m => m.MovementId));
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task GetIncomingPayments_EmptyPeriod_ReturnsNothing()
    {
        // Arrange: records may also come as null
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, """{"id":0,"count":0,"totalCount":0,"records":null}""");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Theory]
    [InlineData(3600, 3600 - 5 * 60 - 1, 1)]
    [InlineData(3600, 3600 - 5 * 60, 2)]
    [InlineData(300, 149, 1)]
    [InlineData(300, 150, 2)]
    public async Task GetIncomingPayments_ReusesTheTokenUntilFiveMinutesOrHalfItsLifeBeforeItExpires(int expiresIn,
        int secondsLater, int expectedTokenRequests)
    {
        // Arrange: a 5-minute token is renewed at half its life
        string tokenJson = $$"""{"access_token":"tok-1","expires_in":{{expiresIn}},"token_type":"Bearer"}""";
        BogBonlineClient client = CreateClient();
        _handler.Respond(HttpStatusCode.OK, tokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(1, 0));
        await client.GetIncomingPayments(From, To, default);
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now.AddSeconds(secondsLater));
        _handler.Respond(HttpStatusCode.OK, tokenJson);
        _handler.Respond(HttpStatusCode.OK, Statement(1, 0));

        // Act
        await client.GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(expectedTokenRequests, _handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task GetIncomingPayments_Unauthorized_FailsAndTheNextCallTakesANewToken()
    {
        // Arrange
        BogBonlineClient client = CreateClient();
        _handler.Respond(HttpStatusCode.OK, """{"access_token":"tok-1","expires_in":3600}""");
        _handler.Respond(HttpStatusCode.Unauthorized, """{"Message":"Authorization has been denied"}""");

        // Act
        Result<IReadOnlyList<BankMovement>> first = await client.GetIncomingPayments(From, To, default);
        _handler.Respond(HttpStatusCode.OK, """{"access_token":"tok-2","expires_in":3600}""");
        _handler.Respond(HttpStatusCode.OK, Statement(1, 0));
        Result<IReadOnlyList<BankMovement>> second = await client.GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal("ბანკის API-ს შეცდომა: HTTP 401 - Authorization has been denied", first.Error.Description);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, _handler.Requests.Count(r => r.Method == HttpMethod.Post));
        Assert.Equal("Bearer tok-2", _handler.Requests[^1].Authorization);
    }

    [Fact]
    public async Task GetIncomingPayments_KeycloakError_ReturnsItsCodeAndDescription()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.Unauthorized,
            """{"error":"unauthorized_client","error_description":"Invalid client secret"}""");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankApiError), result.Error.Code);
        Assert.Equal("ბანკის API-ს შეცდომა: HTTP 401 unauthorized_client Invalid client secret",
            result.Error.Description);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task GetIncomingPayments_NonJsonError_ReturnsTheHttpStatus()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.InternalServerError, "oops");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal("ბანკის API-ს შეცდომა: HTTP 500 - -", result.Error.Description);
    }

    [Fact]
    public async Task GetIncomingPayments_RateLimited_ReturnsTheSecondsToWait()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.TooManyRequests, "",
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42)));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankRateLimitExceeded), result.Error.Code);
        Assert.Equal("ბანკი ასე ხშირ მოთხოვნას არ იღებს: სცადეთ 42 წამში", result.Error.Description);
    }

    [Fact]
    public async Task GetIncomingPayments_RateLimitedUntilADate_RoundsTheSecondsUp()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.TooManyRequests, "",
            response => response.Headers.RetryAfter =
                new RetryConditionHeaderValue(Now.AddSeconds(30).AddMilliseconds(200)));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal("ბანკი ასე ხშირ მოთხოვნას არ იღებს: სცადეთ 31 წამში", result.Error.Description);
    }

    [Fact]
    public async Task GetIncomingPayments_RateLimitedWithoutRetryAfter_AsksForAMinute()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.TooManyRequests, "");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal("ბანკი ასე ხშირ მოთხოვნას არ იღებს: სცადეთ ერთ წუთში", result.Error.Description);
    }

    [Theory]
    [InlineData("""{"token_type":"Bearer","expires_in":300}""")]
    [InlineData("""{"access_token":"tok","expires_in":0}""")]
    [InlineData("null")]
    [InlineData("{not json")]
    public async Task GetIncomingPayments_InvalidTokenResponse_Fails(string tokenJson)
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, tokenJson);

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankResponseIsInvalid), result.Error.Code);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task GetIncomingPayments_InvalidStatement_Fails()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, "null");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankResponseIsInvalid), result.Error.Code);
    }

    [Fact]
    public async Task GetIncomingPayments_Unreachable_Fails()
    {
        // Arrange
        _handler.Throw(new HttpRequestException("no route"));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankIsUnreachable), result.Error.Code);
    }

    [Fact]
    public async Task GetIncomingPayments_Timeout_Fails()
    {
        // Arrange: HttpClient reports its own timeout as a TaskCanceledException
        _handler.Throw(new TaskCanceledException("timeout"));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankIsUnreachable), result.Error.Code);
    }

    [Fact]
    public async Task GetIncomingPayments_CancelledByTheCaller_Throws()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Throw(new TaskCanceledException("cancelled"));

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient().GetIncomingPayments(From, To, cancellation.Token));
    }

    [Theory]
    [InlineData(null, BaseUrl, "id", "secret", Iban)]
    [InlineData(TokenUrl, " ", "id", "secret", Iban)]
    [InlineData(TokenUrl, BaseUrl, null, "secret", Iban)]
    [InlineData(TokenUrl, BaseUrl, "id", " ", Iban)]
    [InlineData(TokenUrl, BaseUrl, "id", "secret", null)]
    public async Task GetIncomingPayments_NotConfigured_FailsWithoutCallingTheBank(string? tokenHost, string? apiHost,
        string? clientId, string? clientSecret, string? accountIban)
    {
        // Arrange
        _settings.TokenUrl = tokenHost;
        _settings.BaseUrl = apiHost;
        _settings.ClientId = clientId;
        _settings.ClientSecret = clientSecret;
        _settings.AccountIban = accountIban;

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankIsNotConfigured), result.Error.Code);
        Assert.Empty(_handler.Requests);
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? Authorization, string? Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<RecordedRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body, Action<HttpResponseMessage>? configure = null)
        {
            _responses.Enqueue(() =>
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                configure?.Invoke(response);
                return response;
            });
        }

        public void Throw(Exception exception)
        {
            _responses.Enqueue(() => throw exception);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(), body));
            return _responses.Dequeue()();
        }
    }
}
