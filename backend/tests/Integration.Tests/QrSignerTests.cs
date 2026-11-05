using Ticketing.Internal.Domain;
using Xunit;

namespace Integration.Tests;

public class QrSignerTests
{
    private const string Key = "unit-test-qr-key-at-least-thirty-two-bytes-long";
    private readonly Guid _eventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _ticketId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Sign_And_Verify_ValidPayload_Succeeds()
    {
        var signer = new QrSigner(Key);
        var payload = signer.Sign(_ticketId, _eventId);

        var verified = signer.TryVerify(payload, _eventId, out var verifiedTicketId);

        Assert.True(verified);
        Assert.Equal(_ticketId, verifiedTicketId);
        Assert.StartsWith("v1.", payload);
        Assert.Equal(4, payload.Split('.').Length);
        Assert.DoesNotContain("@", payload); // Không chứa email / PII
    }

    [Fact]
    public void Verify_TamperedPayload_Fails()
    {
        var signer = new QrSigner(Key);
        var payload = signer.Sign(_ticketId, _eventId);

        // Thay đổi 1 ký tự cuối trong chữ ký
        var tampered = payload[..^1] + (payload[^1] == 'a' ? 'b' : 'a');

        var verified = signer.TryVerify(tampered, _eventId, out _);

        Assert.False(verified);
    }

    [Fact]
    public void Verify_DifferentEventId_Fails()
    {
        var signer = new QrSigner(Key);
        var payload = signer.Sign(_ticketId, _eventId);

        var anotherEventId = Guid.NewGuid();
        var verified = signer.TryVerify(payload, anotherEventId, out _);

        Assert.False(verified);
    }

    [Fact]
    public void Verify_DifferentSecretKey_Fails()
    {
        var signer1 = new QrSigner(Key);
        var signer2 = new QrSigner(Key + "-different-secret-for-testing");

        var payload = signer1.Sign(_ticketId, _eventId);
        var verified = signer2.TryVerify(payload, _eventId, out _);

        Assert.False(verified);
    }

    [Fact]
    public void Signer_RequiresMinimum32BytesKey()
    {
        Assert.Throws<ArgumentException>(() => new QrSigner("short-key"));
    }
}

