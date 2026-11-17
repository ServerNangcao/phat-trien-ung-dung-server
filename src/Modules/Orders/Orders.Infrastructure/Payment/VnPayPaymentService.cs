using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Orders.Domain.Entities;

namespace Ticketing.Modules.Orders.Infrastructure.Payment;

/// <summary>
/// Configuration for VNPay payment gateway.
/// </summary>
public sealed class VnPayOptions
{
    public string? TmnCode { get; init; }
    public string? HashSecret { get; init; }
    public string? BaseUrl { get; init; }
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// VNPay payment gateway integration (v2.1.0). The HMAC-SHA512 signature is computed over
/// the sorted key=value pairs joined with '&amp;' using the URL-ENCODED values (space as '+',
/// URLSearchParams rules), matching the leading Node reference library
/// (lehuygiang28/vnpay). The official merchant samples hash raw values, but the live sandbox
/// gateway rejected raw-value signing with "Invalid signature" on the first real payment,
/// confirming the encoded form is what the gateway expects.
/// </summary>
public sealed class VnPayPaymentService
{
    private readonly VnPayOptions _options;
    private readonly ILogger<VnPayPaymentService> _logger;

    public VnPayPaymentService(IOptions<VnPayOptions> options, ILogger<VnPayPaymentService> logger)
    {
        _options = options.Value;

        // Fail fast on configuration: an empty HashSecret would silently sign with an empty key.
        if (string.IsNullOrWhiteSpace(_options.HashSecret) ||
            string.IsNullOrWhiteSpace(_options.TmnCode) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException(
                "VNPay configuration is incomplete: Payment:VnPay:TmnCode, HashSecret and BaseUrl are required.");
        }

        _logger = logger;
    }

    /// <summary>
    /// Generate VNPay payment URL with HMAC-SHA512 signature.
    /// </summary>
    /// <summary>The VNPay gateway works in GMT+7 (no DST in Vietnam); its timestamps
    /// (<c>vnp_CreateDate</c>, <c>vnp_ExpireDate</c>, IPN <c>vnp_PayDate</c>) are wall
    /// times in that zone. Sending UTC makes the gateway see the transaction as 7 hours
    /// old and fail it with payment-page error code 15 (exceeded waiting time).</summary>
    private static readonly TimeSpan GatewayOffset = TimeSpan.FromHours(7);

    public string GeneratePaymentUrl(Guid orderId, decimal amount, string orderDescription, string? ipAddress)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(GatewayOffset);
        var timeStamp = now.ToString("yyyyMMddHHmmss");
        var expire = now.AddMinutes(15).ToString("yyyyMMddHHmmss");

        var data = new Dictionary<string, string>
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", _options.TmnCode! },
            { "vnp_Locale", "vn" },
            { "vnp_CurrCode", "VND" },
            { "vnp_Amount", ((long)(amount * 100)).ToString() },
            { "vnp_TxnRef", orderId.ToString() },
            { "vnp_OrderInfo", orderDescription },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", _options.ReturnUrl! },
            { "vnp_IpAddr", ipAddress ?? "127.0.0.1" },
            { "vnp_ExpireDate", expire },
            { "vnp_CreateDate", timeStamp }
        };

        var signature = SignData(data);
        var queryString = string.Join("&", data.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{EncodeVnpayValue(kvp.Key)}={EncodeVnpayValue(kvp.Value)}"));
        var paymentUrl = $"{_options.BaseUrl}?{queryString}&vnp_SecureHash={signature}";

        _logger.LogInformation("Generated VNPay payment URL for order {OrderId}", orderId);
        return paymentUrl;
    }

    /// <summary>
    /// Verify VNPay callback signature. Callback values are URL-decoded (e.g. from
    /// HttpRequest.Query); the signature is computed over the URL-ENCODED form with space as '+'
    /// (URLSearchParams rules) to match what the gateway signs.
    /// </summary>
    public bool VerifyCallbackSignature(Dictionary<string, string?> callbackData, string secureHash)
    {
        var sortedData = callbackData
            .Where(kvp => kvp.Key != "vnp_SecureHash" && !string.IsNullOrEmpty(kvp.Value))
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={EncodeVnpayValue(kvp.Value!)}");

        var queryString = string.Join("&", sortedData);
        var calculatedHash = GenerateHmacSha512(_options.HashSecret!, queryString);

        // Constant-time comparison: the hash is a secret-derived value.
        var isValid = FixedTimeEquals(calculatedHash, secureHash);

        if (!isValid)
        {
            // Never log the attacker-controlled hash value itself (log forging).
            _logger.LogWarning("VNPay callback signature verification failed");
        }

        return isValid;
    }

    /// <summary>
    /// Parse VNPay callback data into a structured response.
    /// Accepts potentially-null values from HTTP query string; missing fields default reasonably.
    /// </summary>
    public VnPayCallbackResponse ParseCallback(Dictionary<string, string?> callbackData)
    {
        callbackData.TryGetValue("vnp_TxnRef", out var txnRef);
        callbackData.TryGetValue("vnp_Amount", out var amountStr);
        callbackData.TryGetValue("vnp_TransactionStatus", out var status);
        callbackData.TryGetValue("vnp_BankCode", out var bankCode);
        callbackData.TryGetValue("vnp_PayDate", out var dateStr);
        callbackData.TryGetValue("vnp_ResponseCode", out var responseCode);
        callbackData.TryGetValue("vnp_SecureHash", out var secureHash);

        return new VnPayCallbackResponse
        {
            TransactionId = Guid.TryParse(txnRef, out var txnId) ? txnId : Guid.Empty,
            Amount = decimal.TryParse(amountStr, System.Globalization.CultureInfo.InvariantCulture, out var amount) ? amount / 100m : 0m,
            TransactionStatus = status ?? string.Empty,
            BankCode = bankCode ?? string.Empty,
            TransactionDate = DateTime.TryParseExact(dateStr, "yyyyMMddHHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date) ? date : DateTime.MinValue,
            ResponseCode = responseCode ?? string.Empty,
            SecureHash = secureHash ?? string.Empty
        };
    }

    private string SignData(Dictionary<string, string> data)
    {
        var queryString = string.Join("&", data
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={EncodeVnpayValue(kvp.Value)}"));

        return GenerateHmacSha512(_options.HashSecret!, queryString);
    }

    /// <summary>URL-encodes a value the way URLSearchParams does: space as '+', not '%20'.
    /// Uri.EscapeDataString uses %20, which the VNPay gateway rejects as an invalid signature.</summary>
    private static string EncodeVnpayValue(string value)
    {
        return Uri.EscapeDataString(value).Replace("%20", "+");
    }

    private static string GenerateHmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.ASCII.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

public record VnPayCallbackResponse
{
    public Guid TransactionId { get; init; }
    public decimal Amount { get; init; }
    public string TransactionStatus { get; init; } = string.Empty;
    public string BankCode { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public string ResponseCode { get; init; } = string.Empty;
    public string SecureHash { get; init; } = string.Empty;
}
