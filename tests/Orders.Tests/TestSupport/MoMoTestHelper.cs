using System.Security.Cryptography;
using System.Text;

namespace Ticketing.Tests.Orders.TestSupport;

/// <summary>
/// Independent implementation of MoMo v2 HMAC-SHA256 signing (documented fixed field
/// order joined with '$'), written from the spec rather than from the service under test.
/// </summary>
public static class MoMoTestHelper
{
    public const string PartnerCode = "MOMOTEST";
    public const string AccessKey = "TESTACCESSKEY";
    public const string SecretKey = "TESTSECRETKEY";

    /// <summary>IPN rawString: accessKey$amount$extraData$message$orderId$orderInfo$payType$
    /// partnerCode$requestId$responseTime$resultCode$transId.</summary>
    public static string SignIpn(IReadOnlyDictionary<string, string?> ipn)
    {
        string Get(string key) => ipn.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

        var rawString = $"{AccessKey}${Get("amount")}${Get("extraData")}${Get("message")}${Get("orderId")}" +
                        $"${Get("orderInfo")}${Get("payType")}${PartnerCode}${Get("requestId")}" +
                        $"${Get("responseTime")}${Get("resultCode")}${Get("transId")}";

        return HmacSha256Hex(rawString);
    }

    public static string HmacSha256Hex(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
