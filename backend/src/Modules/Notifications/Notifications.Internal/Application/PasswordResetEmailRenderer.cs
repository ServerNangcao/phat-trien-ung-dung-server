using System.Globalization;
using System.Net;

namespace Notifications.Internal.Application;

/// <summary>
/// Tạo nội dung HTML cho email mã OTP đặt lại mật khẩu.
/// </summary>
internal static class PasswordResetEmailRenderer
{
    public static string RenderPasswordResetOtpHtml(
        string fullName,
        string otpCode,
        int expiresInMinutes)
    {
        var safeName = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(fullName) ? "Quý khách" : fullName.Trim());
        var safeOtp = WebUtility.HtmlEncode(otpCode.Trim());
        var minutesText = expiresInMinutes.ToString(CultureInfo.InvariantCulture);

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Mã xác nhận đặt lại mật khẩu</title>
            </head>
            <body style="margin:0;padding:0;background-color:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#182226;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:24px 12px;background-color:#f4f4f5;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="600" cellspacing="0" cellpadding="0" style="max-width:600px;width:100%;background-color:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e2e8f0;">
                      <tr>
                        <td style="background-color:#F05B45;padding:24px 28px;color:#ffffff;">
                          <div style="font-size:22px;font-weight:800;letter-spacing:-0.02em;">ticketing.</div>
                          <div style="margin-top:6px;font-size:14px;opacity:0.95;">Xác nhận đặt lại mật khẩu tài khoản</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;">
                          <p style="margin:0 0 12px 0;font-size:15px;line-height:1.6;">Xin chào <strong>{{safeName}}</strong>,</p>
                          <p style="margin:0 0 20px 0;font-size:14px;line-height:1.6;color:#475569;">
                            Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng nhập mã xác nhận dưới đây để tiếp tục:
                          </p>
                          <div style="margin:0 0 20px 0;padding:20px;background-color:#f8fafc;border:1px dashed #cbd5e1;border-radius:12px;text-align:center;">
                            <div style="font-size:11px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:#64748b;margin-bottom:8px;">MÃ XÁC NHẬN CỦA BẠN</div>
                            <div style="font-family:monospace;font-size:32px;font-weight:800;letter-spacing:8px;color:#182226;">{{safeOtp}}</div>
                          </div>
                          <p style="margin:0 0 12px 0;font-size:14px;line-height:1.6;color:#475569;">
                            Mã xác nhận này có hiệu lực trong vòng <strong>{{minutesText}} phút</strong> và chỉ được sử dụng một lần.
                          </p>
                          <p style="margin:0;font-size:13px;line-height:1.6;color:#64748b;">
                            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email và không chia sẻ mã xác nhận cho bất kỳ ai.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px;background-color:#f8fafc;border-top:1px solid #e2e8f0;font-size:12px;color:#64748b;">
                          Email được gửi tự động từ hệ thống Ticketing Platform. Vui lòng không trả lời email này.
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
}
