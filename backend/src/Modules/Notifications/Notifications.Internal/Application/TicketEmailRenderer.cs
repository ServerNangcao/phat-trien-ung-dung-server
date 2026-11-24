using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;
using Ticketing.Contracts;

namespace Notifications.Internal.Application;

/// <summary>
/// Tạo nội dung HTML vé điện tử kèm mã QR PNG nhúng inline (base64 data URI) để SmtpEmailService chuyển thành CID.
/// </summary>
internal static class TicketEmailRenderer
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string RenderTicketIssuedHtml(TicketIssuedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var customerName = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(integrationEvent.CustomerName) ? "Quý khách" : integrationEvent.CustomerName);
        var eventTitle = WebUtility.HtmlEncode(integrationEvent.EventTitle);
        var orderCode = $"ORD-{integrationEvent.OrderId.ToString("N")[..8].ToUpperInvariant()}";
        var totalAmount = integrationEvent.Tickets.Sum(t => t.Price).ToString("N0", ViCulture);

        var ticketsHtml = new StringBuilder();
        foreach (var ticket in integrationEvent.Tickets)
        {
            var qrBytes = GenerateQrPngBytes(ticket.QrPayload);
            var qrBase64 = Convert.ToBase64String(qrBytes);
            var ticketTypeName = WebUtility.HtmlEncode(ticket.TicketTypeName);
            var ticketCode = WebUtility.HtmlEncode(ticket.TicketCode);
            var priceFormatted = ticket.Price.ToString("N0", ViCulture);

            ticketsHtml.Append(CultureInfo.InvariantCulture, $$"""
                <tr>
                  <td style="padding:0 0 16px 0;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #e2e8f0;border-radius:12px;background-color:#f8fafc;">
                      <tr>
                        <td style="padding:16px 20px;vertical-align:top;">
                          <div style="font-size:11px;font-weight:700;letter-spacing:0.06em;text-transform:uppercase;color:#F05B45;">VÉ ĐIỆN TỬ</div>
                          <div style="margin-top:4px;font-size:17px;font-weight:700;color:#182226;">{{ticketTypeName}}</div>
                          <div style="margin-top:8px;font-size:13px;color:#475569;">Mã vé: <strong style="font-family:monospace;color:#182226;">{{ticketCode}}</strong></div>
                          <div style="margin-top:4px;font-size:13px;color:#475569;">Giá vé: <strong style="color:#182226;">{{priceFormatted}} đ</strong></div>
                        </td>
                        <td width="164" align="center" style="padding:16px 20px 16px 8px;vertical-align:middle;">
                          <img src="data:image/png;base64,{{qrBase64}}" width="140" height="140" alt="QR {{ticketCode}}" style="display:block;border:1px solid #e2e8f0;border-radius:8px;background:#ffffff;padding:6px;" />
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
                """);
        }

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Vé điện tử - {{eventTitle}}</title>
            </head>
            <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#182226;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:24px 12px;background-color:#f4f4f5;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellspacing="0" cellpadding="0" style="max-width:600px;width:100%;background-color:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e2e8f0;">
                      <tr>
                        <td style="background-color:#F05B45;padding:24px 28px;color:#ffffff;">
                          <div style="font-size:22px;font-weight:800;letter-spacing:-0.02em;">ticketing.</div>
                          <div style="margin-top:6px;font-size:14px;opacity:0.95;">Xác nhận thanh toán &amp; Phát hành vé điện tử</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;">
                          <p style="margin:0 0 12px 0;font-size:15px;line-height:1.6;">Xin chào <strong>{{customerName}}</strong>,</p>
                          <p style="margin:0 0 20px 0;font-size:14px;line-height:1.6;color:#475569;">
                            Đơn hàng <strong style="font-family:monospace;color:#182226;">{{orderCode}}</strong> (tổng thanh toán <strong>{{totalAmount}} đ</strong>) cho sự kiện
                            <strong style="color:#182226;">{{eventTitle}}</strong> đã được xác nhận thành công.
                          </p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                            {{ticketsHtml}}
                          </table>
                          <p style="margin:12px 0 0 0;font-size:13px;line-height:1.6;color:#475569;">
                            Vui lòng xuất trình mã QR ở trên (hoặc trong mục <strong>Vé của tôi</strong> trên website) cho nhân viên soát vé tại cổng tham dự.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px;background-color:#f8fafc;border-top:1px solid #e2e8f0;font-size:12px;color:#64748b;">
                          Email được gửi tự động từ hệ thống Ticketing Platform. Vui lòng không chia sẻ mã QR cho người khác.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    internal static byte[] GenerateQrPngBytes(string payload)
    {
        var safePayload = string.IsNullOrEmpty(payload) ? "INVALID" : payload;
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(safePayload, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(qrData);
        return pngQr.GetGraphic(8);
    }
}
