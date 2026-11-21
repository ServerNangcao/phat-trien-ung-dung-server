using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Infrastructure.Payment;

/// <summary>
/// Configuration for the MoMo e-wallet gateway (FR-16). Unlike VNPay these are validated
/// lazily on first use: the module must start (and the whole VNPay flow must work) on
/// machines that never configure MoMo.
/// </summary>
public sealed class MoMoOptions
{
    public string? PartnerCode { get; init; }
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
    public string? ApiBaseUrl { get; init; }

    /// <summary>Browser redirect after the customer pays.</summary>
    public string? RedirectUrl { get; init; }

    /// <summary>Server-to-server notification endpoint MoMo calls.</summary>
    public string? IpnUrl { get; init; }
}

/// <summary>
/// MoMo e-wallet integration (Payment API v2). Signatures are HMAC-SHA256 hex over the
/// documented fixed field order joined with '$' — raw values, no URL encoding. The create
/// request order is: accessKey, amount, extraData, ipnUrl, orderId, orderInfo, partnerCode,
/// redirectUrl, requestId, requestType. The IPN order is: accessKey, amount, extraData,
/// message, orderId, orderInfo, payType, partnerCode, requestId, responseTime, resultCode,
/// transId. Orders only reach the gateway in the Pending state; the payment completion
/// itself goes through the shared CompleteOrderPaymentCommand state machine.
/// </summary>
public sealed class MoMoPaymentService
{
    private readonly MoMoOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MoMoPaymentService> _logger;

    public MoMoPaymentService(IOptions<MoMoOptions> options, IHttpClientFactory httpClientFactory,
        ILogger<MoMoPaymentService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Create a MoMo payment and return the hosted payment URL. Fails with a
    /// configuration error when MoMo credentials are absent, so the operator sees the
    /// real cause instead of a signature mystery.
    /// </summary>
    public async Task<Result<MoMoPaymentResult>> CreatePaymentAsync(Guid orderId, decimal amount,
        string orderInfo, CancellationToken cancellationToken = default)
    {
        var configError = ValidateConfigured();
        if (configError is not null)
            return Result<MoMoPaymentResult>.Failure(configError.Value);

        var requestId = $"{_options.PartnerCode}-{orderId:N}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var amountVnd = ((long)amount).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var payload = new Dictionary<string, string>
        {
            ["partnerCode"] = _options.PartnerCode!,
            ["requestId"] = requestId,
            ["amount"] = amountVnd,
            ["orderId"] = orderId.ToString(),
            ["orderInfo"] = orderInfo,
            ["redirectUrl"] = _options.RedirectUrl ?? string.Empty,
            ["ipnUrl"] = _options.IpnUrl ?? string.Empty,
            ["requestType"] = "captureWallet",
            ["extraData"] = string.Empty,
            ["lang"] = "vi"
        };

        // Documented create rawString: accessKey$amount$extraData$ipnUrl$orderId$orderInfo$
        // partnerCode$redirectUrl$requestId$requestType (extraData empty).
        var createRawString = string.Join('$', _options.AccessKey ?? string.Empty, amountVnd, string.Empty,
            _options.IpnUrl ?? string.Empty, orderId.ToString(), orderInfo, _options.PartnerCode!,
            _options.RedirectUrl ?? string.Empty, requestId, "captureWallet");
        payload["signature"] = HmacSha256Hex(_options.SecretKey!, createRawString);

        try
        {
            var client = _httpClientFactory.CreateClient("momo");
            using var response = await client.PostAsJsonAsync(
                $"{_options.ApiBaseUrl!.TrimEnd('/')}/v2/gateway/create", payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return Result<MoMoPaymentResult>.Failure(Error.Internal(
                    $"MoMo create returned HTTP {(int)response.StatusCode}."));

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (body.TryGetProperty("resultCode", out var resultCodeEl) && resultCodeEl.GetInt32() != 0)
            {
                var message = body.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                return Result<MoMoPaymentResult>.Failure(Error.Internal($"MoMo create failed: {message ?? "unknown error"}."));
            }

            var payUrl = body.TryGetProperty("payUrl", out var payUrlEl) ? payUrlEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(payUrl))
                return Result<MoMoPaymentResult>.Failure(Error.Internal("MoMo create returned no payUrl."));

            _logger.LogInformation("Generated MoMo payment URL for order {OrderId}", orderId);
            return Result<MoMoPaymentResult>.Success(new MoMoPaymentResult(
                orderId, requestId, payUrl,
                body.TryGetProperty("qrCodeUrl", out var qrEl) ? qrEl.GetString() : null));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<MoMoPaymentResult>.Failure(Error.Internal($"MoMo gateway unreachable: {ex.Message}"));
        }
    }

    /// <summary>
    /// Verify a MoMo IPN signature. Body values arrive URL-decoded; the rawString uses raw
    /// values in the documented IPN field order.
    /// </summary>
    public bool VerifyIpnSignature(IReadOnlyDictionary<string, string?> ipn, string signature)
    {
        string Get(string key) => ipn.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

        var rawString = $"{_options.AccessKey}${Get("amount")}${Get("extraData")}${Get("message")}${Get("orderId")}" +
                        $"${Get("orderInfo")}${Get("payType")}${_options.PartnerCode}${Get("requestId")}" +
                        $"${Get("responseTime")}${Get("resultCode")}${Get("transId")}";

        var expected = HmacSha256Hex(_options.SecretKey ?? string.Empty, rawString);
        return FixedTimeEquals(expected, signature ?? string.Empty);
    }

    /// <summary>Fail when the gateway credentials are absent, naming every missing key.</summary>
    public Error? ValidateConfigured()
    {
        var missing = new[]
        {
            ("Payment:MoMo:PartnerCode", _options.PartnerCode),
            ("Payment:MoMo:AccessKey", _options.AccessKey),
            ("Payment:MoMo:SecretKey", _options.SecretKey),
            ("Payment:MoMo:ApiBaseUrl", _options.ApiBaseUrl)
        }.Where(kv => string.IsNullOrWhiteSpace(kv.Item2)).Select(kv => kv.Item1).ToList();

        return missing.Count == 0
            ? null
            : Error.Internal($"MoMo configuration is incomplete: {string.Join(", ", missing)} are required.");
    }

    internal static string HmacSha256Hex(string secretKey, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

public sealed record MoMoPaymentResult(Guid OrderId, string RequestId, string PayUrl, string? QrCodeUrl);
