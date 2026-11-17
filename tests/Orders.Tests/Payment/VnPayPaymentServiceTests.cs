using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Orders.Infrastructure.Payment;
using Ticketing.Tests.Orders.TestSupport;

namespace Ticketing.Tests.Orders.Payment;

public class VnPayPaymentServiceTests
{
    private static VnPayPaymentService CreateService() => new(
        Options.Create(new VnPayOptions
        {
            TmnCode = VnPayTestHelper.TmnCode,
            HashSecret = VnPayTestHelper.HashSecret,
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ReturnUrl = "http://localhost:5001/api/payments/vnpay/return"
        }),
        NullLogger<VnPayPaymentService>.Instance);

    [Fact]
    public void GeneratePaymentUrl_ContainsRequiredParamsAndScaledAmount()
    {
        var service = CreateService();
        var orderId = Guid.NewGuid();

        var url = service.GeneratePaymentUrl(orderId, 250_000m, "Ticketing order", "127.0.0.1");

        url.Should().StartWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?");
        url.Should().Contain($"vnp_TxnRef={orderId}");
        url.Should().Contain("vnp_Amount=25000000"); // VND × 100
        url.Should().Contain("vnp_SecureHash=");
        url.Should().Contain("vnp_ReturnUrl=http%3A%2F%2Flocalhost%3A5001%2Fapi%2Fpayments%2Fvnpay%2Freturn");
    }

    [Fact]
    public void GeneratePaymentUrl_SignatureMatchesIndependentHmac()
    {
        var service = CreateService();
        var orderId = Guid.NewGuid();

        var url = service.GeneratePaymentUrl(orderId, 250_000m, "order info", "127.0.0.1");

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(url).Query);
        var secureHash = query["vnp_SecureHash"].ToString();
        var data = query.Where(kvp => kvp.Key != "vnp_SecureHash")
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value.ToString());
        var expected = VnPayTestHelper.Sign(data);

        secureHash.Should().Be(expected);
    }

    [Fact]
    public void VerifyCallbackSignature_AcceptsValidSignature()
    {
        var service = CreateService();
        var orderId = Guid.NewGuid();
        var data = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = orderId.ToString(),
            ["vnp_Amount"] = "25000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_PayDate"] = "20261104120000"
        };
        data["vnp_SecureHash"] = VnPayTestHelper.Sign(data);

        service.VerifyCallbackSignature(data, data["vnp_SecureHash"]!).Should().BeTrue();
    }

    [Fact]
    public void VerifyCallbackSignature_RejectsTamperedPayload()
    {
        var service = CreateService();
        var orderId = Guid.NewGuid();
        var data = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = orderId.ToString(),
            ["vnp_Amount"] = "25000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_PayDate"] = "20261104120000"
        };
        data["vnp_SecureHash"] = VnPayTestHelper.Sign(data);
        data["vnp_Amount"] = "99999999"; // tampered after signing

        service.VerifyCallbackSignature(data, data["vnp_SecureHash"]!).Should().BeFalse();
    }

    [Fact]
    public void ParseCallback_MapsFieldsAndScalesAmountBack()
    {
        var service = CreateService();
        var orderId = Guid.NewGuid();
        var data = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = orderId.ToString(),
            ["vnp_Amount"] = "25000000",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_BankCode"] = "NCB",
            ["vnp_PayDate"] = "20261104120000",
            ["vnp_SecureHash"] = "abc123"
        };

        var callback = service.ParseCallback(data);

        callback.TransactionId.Should().Be(orderId);
        callback.Amount.Should().Be(250_000m); // back from ×100
        callback.ResponseCode.Should().Be("00");
        callback.SecureHash.Should().Be("abc123");
    }
}
