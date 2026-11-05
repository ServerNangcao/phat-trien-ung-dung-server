using System.Security.Cryptography;
using System.Text;

namespace Ticketing.Internal.Domain;

/// <summary>
/// Ký số và xác thực mã QR vé điện tử bằng HMAC-SHA256 (FR-17, FR-20).
/// Payload định dạng v1.{ticketId}.{eventId}.{hmac} không chứa thông tin cá nhân (PII).
/// Sử dụng thuật toán FixedTimeEquals để chống tấn công Timing Attack.
/// </summary>
internal sealed class QrSigner
{
    private readonly byte[] _key;

    public QrSigner(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new ArgumentException("QR signing key must contain at least 32 UTF-8 bytes.", nameof(key));
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string Sign(Guid ticketId, Guid eventId)
    {
        var data = $"v1.{ticketId:N}.{eventId:N}";
        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(data));
        return $"{data}.{Convert.ToHexStringLower(hash)}";
    }

    public bool TryVerify(string payload, Guid expectedEventId, out Guid ticketId)
    {
        ticketId = default;

        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 256)
        {
            return false;
        }

        var parts = payload.Split('.');
        if (parts.Length != 4 || parts[0] != "v1")
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[1], "N", out ticketId) ||
            !Guid.TryParseExact(parts[2], "N", out var eventId) ||
            eventId != expectedEventId)
        {
            return false;
        }

        try
        {
            var actual = Convert.FromHexString(parts[3]);
            var data = string.Join('.', parts.Take(3));
            var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(data));

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

