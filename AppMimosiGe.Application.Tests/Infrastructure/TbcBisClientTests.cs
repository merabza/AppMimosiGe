using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Infrastructure.BankTransactions.TbcBis;
using AppMimosiGeShared.Contracts.Errors;
using Moq;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class TbcBisClientTests : IDisposable
{
    private const string TokenJson = """{"access_token":"tok-1","token_type":"Bearer","expires_in":3600}""";
    private static readonly DateOnly From = new(2026, 9, 1);
    private static readonly DateOnly To = new(2026, 9, 30);
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly Mock<TimeProvider> _timeProvider = new();

    private readonly TbcBisSettings _settings = new()
    {
        BaseUrl = "https://test-api.tbcbank.ge",
        ClientId = "id",
        ClientSecret = "secret",
        AccountIban = "GE48TB7873440574631292"
    };

    public TbcBisClientTests()
    {
        _httpClient = new HttpClient(_handler);
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    private TbcBisClient CreateClient()
    {
        return new TbcBisClient(_httpClient, _settings, _timeProvider.Object);
    }

    // the integrated account needs both the account record and the start date
    [Fact]
    public void Account_IsSetOnlyWithTheAccountAndTheStartDate()
    {
        DateTime startDate = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Null(CreateClient().Account);
        _settings.BankAccountId = 4;
        Assert.Null(CreateClient().Account);
        _settings.StartDate = startDate;
        Assert.Equal(new IntegratedBankAccount(4, startDate), CreateClient().Account);
        _settings.BankAccountId = null;
        Assert.Null(CreateClient().Account);
    }

    private static string Movement(string id, bool isDebit = false, int transactionType = 20, int statusCode = 0)
    {
        return $$"""
                 {"movementId":"{{id}}","paymentId":null,"externalPaymentId":"1","amount":150.5,"currency":"GEL",
                  "purpose":"სწავლის საფასური","isDebit":{{(isDebit ? "true" : "false")}},
                  "customerAccountInfo":{"accountName":"Mimosi","accountIban":"GE48TB7873440574631292"},
                  "documentDate":"2026-09-14T00:00:00","exchangeRate":null,"additionalInformation":"info",
                  "opCode":"GIB","partnerAccountInfo":{"accountIban":"GE61TB9876543210987654","bankCode":"TBCBGE22",
                  "bankName":"JSC TBC Bank","treasuryCode":null,"partnerName":"Partner Name","docTypeId":null,
                  "personalNumber":"01001012345","documentNumber":null,"taxCode":"01001054321"},
                  "statusCode":{{statusCode}},"date":"2026-09-15T00:00:00","parentExternalPaymentId":0,
                  "transactionReference":null,"additionalDescription":"descr","documentNumber":"DOC-1",
                  "chargeDetail":null,"taxpayerCode":null,"taxpayerName":null,"transactionType":{{transactionType}}}
                 """;
    }

    private static string Page(int total, params string[] movements)
    {
        return $$"""{"transactions":[{{string.Join(",", movements)}}],"pager":[{"pageIndex":0,"pageSize":700}],"total":{{total}}}""";
    }

    [Fact]
    public async Task GetIncomingPayments_RequestsATokenWithClientCredentialsAndTheMovementsWithIt()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(1, Movement("018.2")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, _handler.Requests.Count);
        RecordedRequest token = _handler.Requests[0];
        Assert.Equal(HttpMethod.Post, token.Method);
        Assert.Equal("https://test-api.tbcbank.ge/oauth/token", token.Uri);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("id:secret"))}", token.Authorization);
        Assert.Equal("grant_type=client_credentials&scope=bab_accounts", token.Body);
        RecordedRequest movements = _handler.Requests[1];
        Assert.Equal(HttpMethod.Get, movements.Method);
        Assert.Equal(
            "https://test-api.tbcbank.ge/bab/v1/accounts/movements?AccountNumber=GE48TB7873440574631292&AccountCurrencyCode=GEL&PeriodFrom=2026-09-01T00:00:00&PeriodTo=2026-09-30T23:59:59&PageIndex=0&PageSize=700",
            movements.Uri);
        Assert.Equal("Bearer tok-1", movements.Authorization);
    }

    [Fact]
    public async Task GetIncomingPayments_MapsTheMovementFields()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(1, Movement("018.2")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(
            new BankMovement("018.2", new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified),
                new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Unspecified), 150.5m, "GEL",
                "სწავლის საფასური", "info", "descr", "DOC-1", "Partner Name", "GE61TB9876543210987654", "TBCBGE22",
                "01001054321", "01001012345"), Assert.Single(result.Value));
    }

    [Fact]
    public async Task GetIncomingPayments_KeepsOnlyAuthorizedIncomingCredits()
    {
        // Arrange: outgoing, own-account transfer, waiting for authorization, then the one incoming credit
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK,
            Page(5, Movement("1.1", true), Movement("2.2", transactionType: 1), Movement("3.2", statusCode: 1),
                Movement("4.2", statusCode: 2), Movement("5.2")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(["5.2"], result.Value.Select(m => m.MovementId));
    }

    [Fact]
    public async Task GetIncomingPayments_ReadsEveryPageUntilTheTotal()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(3, Movement("1.2"), Movement("2.2")));
        _handler.Respond(HttpStatusCode.OK, Page(3, Movement("3.2")));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert: one token for both pages
        Assert.Equal(["1.2", "2.2", "3.2"], result.Value.Select(m => m.MovementId));
        Assert.Equal(3, _handler.Requests.Count);
        Assert.EndsWith("&PageIndex=0&PageSize=700", _handler.Requests[1].Uri, StringComparison.Ordinal);
        Assert.EndsWith("&PageIndex=1&PageSize=700", _handler.Requests[2].Uri, StringComparison.Ordinal);
        Assert.Equal("Bearer tok-1", _handler.Requests[2].Authorization);
    }

    [Fact]
    public async Task GetIncomingPayments_StopsAtAnEmptyPage()
    {
        // Arrange: the total promises more, but the page is empty
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(10, Movement("1.2")));
        _handler.Respond(HttpStatusCode.OK, Page(10));

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(["1.2"], result.Value.Select(m => m.MovementId));
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task GetIncomingPayments_EmptyPeriod_ReturnsNothing()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, """{"transactions":[],"pager":[],"total":0}""");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData(3600 - 5 * 60 - 1, 1)]
    [InlineData(3600 - 5 * 60, 2)]
    public async Task GetIncomingPayments_ReusesTheTokenUntilFiveMinutesBeforeItExpires(int secondsLater,
        int expectedTokenRequests)
    {
        // Arrange
        TbcBisClient client = CreateClient();
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(0));
        await client.GetIncomingPayments(From, To, default);
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now.AddSeconds(secondsLater));
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(0));

        // Act
        await client.GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(expectedTokenRequests, _handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task GetIncomingPayments_Unauthorized_FailsAndTheNextCallTakesANewToken()
    {
        // Arrange
        TbcBisClient client = CreateClient();
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.Unauthorized,
            """{"type":"/error-codes#Unauthorized","title":"Unauthorized access","code":"Unauthorized","status":401}""");

        // Act
        Result<IReadOnlyList<BankMovement>> first = await client.GetIncomingPayments(From, To, default);
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.OK, Page(0));
        Result<IReadOnlyList<BankMovement>> second = await client.GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(BankTransactionErrors.BankApiError(401, "Unauthorized", "Unauthorized access").Description,
            first.Error.Description);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, _handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task GetIncomingPayments_ProblemResponse_ReturnsTheBankCodeAndTitle()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, TokenJson);
        _handler.Respond(HttpStatusCode.Forbidden,
            """{"type":"/error-codes#ForbiddenError","title":"NOT_AUTHORIZED","status":403,"code":"ForbiddenError","traceId":"00-1"}""");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal(nameof(BankTransactionErrors.BankApiError), result.Error.Code);
        Assert.Equal("ბანკის API-ს შეცდომა: HTTP 403 ForbiddenError NOT_AUTHORIZED", result.Error.Description);
    }

    [Fact]
    public async Task GetIncomingPayments_NonJsonError_ReturnsTheHttpStatus()
    {
        // Arrange: the token endpoint itself fails with a plain-text body
        _handler.Respond(HttpStatusCode.InternalServerError, "oops");

        // Act
        Result<IReadOnlyList<BankMovement>> result = await CreateClient().GetIncomingPayments(From, To, default);

        // Assert
        Assert.Equal("ბანკის API-ს შეცდომა: HTTP 500 - -", result.Error.Description);
        Assert.Single(_handler.Requests);
    }

    [Theory]
    [InlineData("""{"token_type":"Bearer","expires_in":3600}""")]
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
    [InlineData(null, "id", "secret", "GE48TB7873440574631292")]
    [InlineData("https://test-api.tbcbank.ge", " ", "secret", "GE48TB7873440574631292")]
    [InlineData("https://test-api.tbcbank.ge", "id", "", "GE48TB7873440574631292")]
    [InlineData("https://test-api.tbcbank.ge", "id", "secret", null)]
    public async Task GetIncomingPayments_NotConfigured_FailsWithoutCallingTheBank(string? host, string? clientId,
        string? clientSecret, string? accountIban)
    {
        // Arrange
        _settings.BaseUrl = host;
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

        public void Respond(HttpStatusCode status, string body)
        {
            _responses.Enqueue(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
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
