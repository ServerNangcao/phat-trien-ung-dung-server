using QRCoder;

namespace Ticketing.Modules.Notifications.Application.Services;

/// <summary>
/// Generates an embedded QR code when the Ticketing module did not supply one, so the
/// ticket email always carries a scannable code.
/// </summary>
public static class QrCodeImageGenerator
{
    public static string GenerateDataUri(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
