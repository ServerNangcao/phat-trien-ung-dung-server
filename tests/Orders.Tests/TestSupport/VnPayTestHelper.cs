using System.Security.Cryptography;
using System.Text;

namespace Ticketing.Tests.Orders.TestSupport;

/// <summary>
/// Computes VNPay HMAC-SHA512 signatures from an independent implementation of the
/// documented algorithm (sorted raw key=value pairs joined with &amp;, RAW values — not
/// URL-encoded), so the service under test is cross-checked against the spec.
/// The secret must match the Orders.Api appsettings used by the WAF tests.
/// </summary>
public static class VnPayTestHelper
{
    public const string TmnCode = "DEMODTMB";
    public const string HashSecret = "DEMOHASHSECRET";

    public static string Sign(IReadOnlyDictionary<string, string?> data)
    {
        var queryString = string.Join("&", data
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value!}"));

        using var hmac = new HMACSHA512(Encoding.ASCII.GetBytes(HashSecret));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(queryString));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>Builds an escaped IPN query string with a valid signature (or a tampered amount).</summary>
    public static string BuildIpnQuery(Guid orderId, decimal amountVnd, bool tamperAmount = false, string transactionStatus = "00")
    {
        var data = new Dictionary<string, string?>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = TmnCode,
            ["vnp_Amount"] = ((long)(amountVnd * 100)).ToString(),
            ["vnp_TxnRef"] = orderId.ToString(),
            ["vnp_OrderInfo"] = $"Ticketing order {orderId}",
            ["vnp_OrderType"] = "other",
            ["vnp_CurrencyCode"] = "VND",
            ["vnp_BankCode"] = "NCB",
            ["vnp_PayDate"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TransactionNo"] = "14000001"
        };

        if (tamperAmount)
            data["vnp_Amount"] = ((long)(amountVnd * 100) + 1).ToString();

        data["vnp_SecureHash"] = Sign(data.Where(kvp => kvp.Key != "vnp_SecureHash").ToDictionary(k => k.Key, k => k.Value));

        return string.Join("&", data.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));
    }
}
