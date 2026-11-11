using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Orders.Infrastructure.Payment;
using Ticketing.Tests.Orders.TestSupport;

namespace Ticketing.Tests.Orders.Payment;

public class MoMoPaymentServiceTests
{
    private const decimal Amount = 250_000m;
    private static readonly Guid OrderId = Guid.NewGuid();

    private static MoMoPaymentService CreateService(HttpMessageHandler handler) =>
        new(Options.Create(new MoMoOptions
        {
            PartnerCode = MoMoTestHelper.PartnerCode,
            AccessKey = MoMoTestHelper.AccessKey,
            SecretKey = MoMoTestHelper.SecretKey,
            ApiBaseUrl = "https://test-payment.momo.test",
            RedirectUrl = "https://app.test/redirect",
            IpnUrl = "https://app.test/api/payments/momo/ipn"
        }),
        new StubHttpClientFactory(new HttpClient(handler)),
        NullLogger<MoMoPaymentService>.Instance);

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    [Fact]
    public async Task CreatePayment_PostsSignedRequestAndReturnsPayUrl()
    {
        Dictionary<string, string>? capturedBody = null;
        var handler = new StubHandler(() =>
        {
            var response = """
                {"payUrl":"https://test-payment.momo.test/pay/abc","qrCodeUrl":"https://qr.test/abc","resultCode":0,"message":"Success"}
                """;
            return (HttpStatusCode.OK, response);
        })
        { OnRequest = body => capturedBody = body };
        var service = CreateService(handler);

        var result = await service.CreatePaymentAsync(OrderId, Amount, "Ticketing order x");

        result.IsSuccess.Should().BeTrue();
        result.Value.PayUrl.Should().Be("https://test-payment.momo.test/pay/abc");
        result.Value.OrderId.Should().Be(OrderId);

        capturedBody.Should().NotBeNull();
        capturedBody!["partnerCode"].Should().Be(MoMoTestHelper.PartnerCode);
        capturedBody["orderId"].Should().Be(OrderId.ToString());
        capturedBody["amount"].Should().Be("250000");
        capturedBody["requestType"].Should().Be("captureWallet");

        // Recompute the signature independently over the documented create rawString.
        var rawString = $"{MoMoTestHelper.AccessKey}${capturedBody["amount"]}${capturedBody["extraData"]}" +
                        $"${capturedBody["ipnUrl"]}${capturedBody["orderId"]}${capturedBody["orderInfo"]}" +
                        $"${capturedBody["partnerCode"]}${capturedBody["redirectUrl"]}${capturedBody["requestId"]}" +
                        $"${capturedBody["requestType"]}";
        capturedBody["signature"].Should().Be(MoMoTestHelper.HmacSha256Hex(rawString));
    }

    [Fact]
    public async Task CreatePayment_WhenGatewayRejects_FailsWithMessage()
    {
        var handler = new StubHandler(() => (HttpStatusCode.OK,
            """{"resultCode":1005,"message":"invalid partner"}"""));
        var service = CreateService(handler);

        var result = await service.CreatePaymentAsync(OrderId, Amount, "info");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("invalid partner");
    }

    [Fact]
    public async Task CreatePayment_WhenUnconfigured_FailsNamingMissingKeys()
    {
        var service = new MoMoPaymentService(
            Options.Create(new MoMoOptions()),
            new StubHttpClientFactory(new HttpClient(new StubHandler(() => (HttpStatusCode.OK, "{}")))),
            NullLogger<MoMoPaymentService>.Instance);

        var result = await service.CreatePaymentAsync(OrderId, Amount, "info");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("PartnerCode").And.Contain("AccessKey").And.Contain("SecretKey");
    }

    [Fact]
    public void VerifyIpnSignature_ValidSignature_ReturnsTrue()
    {
        var service = CreateService(new StubHandler(() => (HttpStatusCode.OK, "{}")));
        var ipn = new Dictionary<string, string?>
        {
            ["partnerCode"] = MoMoTestHelper.PartnerCode,
            ["orderId"] = OrderId.ToString(),
            ["requestId"] = "req-1",
            ["amount"] = "250000",
            ["transId"] = "998877",
            ["payType"] = "web",
            ["signature"] = ""
        };
        ipn["orderInfo"] = "Ticketing order";
        ipn["message"] = "Success";
        ipn["responseTime"] = "1730000000000";
        ipn["resultCode"] = "0";
        ipn["extraData"] = "";
        ipn["signature"] = MoMoTestHelper.SignIpn(ipn);

        service.VerifyIpnSignature(ipn, ipn["signature"]!).Should().BeTrue();
    }

    [Fact]
    public void VerifyIpnSignature_TamperedAmount_ReturnsFalse()
    {
        var service = CreateService(new StubHandler(() => (HttpStatusCode.OK, "{}")));
        var ipn = new Dictionary<string, string?>
        {
            ["partnerCode"] = MoMoTestHelper.PartnerCode,
            ["orderId"] = OrderId.ToString(),
            ["requestId"] = "req-1",
            ["amount"] = "999999",
            ["transId"] = "998877",
            ["payType"] = "web",
            ["orderInfo"] = "Ticketing order",
            ["message"] = "Success",
            ["responseTime"] = "1730000000000",
            ["resultCode"] = "0",
            ["extraData"] = "",
            ["signature"] = ""
        };
        ipn["signature"] = MoMoTestHelper.SignIpn(ipn);
        ipn["amount"] = "1";

        service.VerifyIpnSignature(ipn, ipn["signature"]!).Should().BeFalse();
    }

    private sealed class StubHandler(Func<(HttpStatusCode Status, string Body)> responder) : HttpMessageHandler
    {
        public Action<Dictionary<string, string>?>? OnRequest { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (OnRequest is not null && request.Content is not null)
            {
                var json = await request.Content.ReadAsStringAsync(cancellationToken);
                OnRequest(JsonSerializer.Deserialize<Dictionary<string, string>>(json));
            }

            var (status, body) = responder();
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
