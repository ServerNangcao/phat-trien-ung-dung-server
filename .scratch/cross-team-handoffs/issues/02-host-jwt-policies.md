# 02: Host (TV1) — gắn JWT policy theo bảng endpoint của các module

**What to build:** Host `Ticketing.Api` áp authentication/authorization cho từng endpoint
của module Orders và Notifications theo bảng mapping đã chốt trong `TV2-DELIVERY.md`
(mục "Authorization contract for TV1's host"). Nguyên tắc: khách hàng chỉ truy cập đơn
của chính mình (chặn IDOR), admin duyệt hoàn tiền, callback VNPay giữ anonymous vì chữ ký
HMAC-SHA512 chính là xác thực, và endpoint integration nội bộ cần service token.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

Bảng mapping cần áp (chi tiết từng endpoint trong `TV2-DELIVERY.md`):

| Endpoint | Policy |
| --- | --- |
| `POST /api/orders`, `GET /api/orders/customer/{id}`, `GET /api/orders/{id}`, `POST /api/orders/{id}/refund` | Customer, subject khớp customerId/order owner |
| `GET /api/orders/refunds`, `POST /api/orders/refunds/{id}/process` | Admin |
| `GET /api/orders/events/{eventId}/attendees` | Organizer sở hữu event, hoặc Staff |
| `POST /api/payments/vnpay/ipn`, `GET /api/payments/vnpay/return` | Anonymous (HMAC signature là auth) |
| `POST /api/integration-events/ticket-issued`, `POST /api/notifications/send` | Service token nội bộ |
| `GET /api/notifications/order/{orderId}` | Customer sở hữu đơn, hoặc Admin |

- [ ] Anonymous gọi customer/admin endpoints → 401; token khách A đọc đơn khách B → 403
- [ ] Token Admin duyệt/từ chối refund → 200, và order chuyển trạng thái đúng
- [ ] `POST /api/payments/vnpay/ipn` không cần token vẫn xử lý được (IPN có chữ ký hợp lệ)
- [ ] `POST /api/integration-events/ticket-issued` không service token → 401
- [ ] Endpoint để lại 401/403 đúngcode cho mọi trường hợp trong bảng (không lộ dữ liệu qua lỗi 500)
