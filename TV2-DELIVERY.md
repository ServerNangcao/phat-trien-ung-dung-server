# TV2 Delivery — Orders & Notifications Modules

Scope: TV2 (member 2) of the 5-member team — Module Orders (schema `orders`) and
Module Notifications (schema `notifications`) per `BANG_PHAN_CONG_NHIEM_VU.docx`.

## How to run

```bash
# Both APIs default to PostgreSQL at localhost:5432 (ConnectionStrings:Default).
dotnet run --project src/Modules/Orders/Orders.Api --urls http://localhost:5001
dotnet run --project src/Modules/Notifications/Notifications.Api --urls http://localhost:5002

# Without a database (dev/demo): EF InMemory provider
dotnet run --project src/Modules/Orders/Orders.Api -- --Persistence:Provider=InMemory

# Mock Ticketing module (TV4's contract) for the full payment flow without TV4's host:
node scripts/mock-ticketing.js   # serves GET /api/ticketing/reservations/{id} and /events/{id}/tickets on :5003
```

Tests: `dotnet test` — 64 tests (41 Orders + 23 Notifications pass; 3 Testcontainers
Postgres tests skip when Docker is absent and run in CI with Docker).

## Flow (Orders)

1. `POST /api/orders` `{customerId, reservationId}` — handler fetches the reservation
   through the `ITicketingModuleApi` port (consumer-driven contract; HTTP adapter maps
   TV4's API), computes the total from reservation prices (never client-sent), and is
   idempotent per reservation (one reservation → at most one active order).
   Response: `{orderId, totalAmount, status, paymentUrl}`.
2. `paymentUrl` — VNPay sandbox redirect with HMAC-SHA512 over the sorted, URL-encoded
   params (`vnp_Amount` = VND × 100).
3. VNPay calls `POST /api/payments/vnpay/ipn?...` — signature verified (vnp_SecureHash
   excluded, empty values skipped), amount cross-checked against the order total,
   idempotent replay ack, reservation validity checked via the Ticketing port:
   - active reservation → `MarkAsPaid` → `OrderPaid` domain event
   - expired reservation → `MarkAsPaidAfterExpiry` (ADR-0003) + auto `RefundRequest`
   - Ticketing unreachable → RspCode 99 so VNPay retries (never guess)
   Domain events are mapped to integration events and written to `orders.outbox_messages`
   inside the same transaction as the state change (`OrdersDbContext.SaveChangesAsync`).
4. Outbox delivery: module-local `OutboxDeliveryService` POSTs each event to configured
   subscribers with retry ceiling 5; TV1's central worker scans the same table with
   `FOR UPDATE SKIP LOCKED`.
5. Refunds: `POST /api/orders/{id}/refund` (customer), `GET /api/orders/refunds`,
   `POST /api/orders/refunds/{id}/process` `{approve}` — approve completes
   RefundRequested → Refunded (or PaidAfterExpiry → Refunded for ADR-0003) and writes
   `RefundProcessedIntegrationEvent` to the outbox.
6. Queries: `GET /api/orders/customer/{customerId}` (FR-13), `GET /api/orders/{id}`,
   `GET /api/orders/events/{eventId}/attendees` (FR-22 — joins Ticketing's issued
   tickets with paid orders only).

## Flow (Notifications)

1. Other modules' outbox workers deliver `POST /api/integration-events/ticket-issued`.
   Events without an `EventId` are rejected (400) — dedupe is impossible without one.
   The Inbox pattern stages the event and the Pending email history row, and commits
   them atomically, so a crash after the 202 ack is recovered by the retry worker
   (Pending sweep) instead of losing the email. A redelivered event is answered with
   `{duplicated: true}` and never sends twice.
2. `TicketEmailProcessor` owns the single send lifecycle: renders the HTML template
   (event fields HTML-encoded), includes an invoice table when the event carries
   pricing (`TotalAmount` + `Items`; omitted for older payloads), embeds the QR
   (`QrCodeUrl` from Ticketing, or a locally generated data-URI fallback via QRCoder),
   and marks Sent / Failed + backoff.
3. Delivery budget < 30s: `SmtpEmailService` retries transient failures 3× (2s/4s
   backoff, 8s hard bound per attempt — worst case ≈ 30s); `EmailRetryProcessor`
   (10s worker) retries failed sends at 10s..60s backoff up to 5 attempts and recovers
   Pending emails abandoned by a crash after 30s.
4. `GET /api/notifications/order/{orderId}` exposes history and status.

## Verification evidence

- `dotnet build`: 0 warnings / 0 errors (TreatWarningsAsErrors, nullable enabled).
- `dotnet test`: 64 tests — 64 run green (41 + 23), 3 Testcontainers tests skip without
  Docker. The suite caught two real bugs that were fixed:
  1. Outbox payloads serialized as `{}` (STJ used the `IIntegrationEvent` marker's
     metadata instead of the runtime type).
  2. Refund approval required `Paid` state, but a requested refund puts the order in
     `RefundRequested`; `Refunded` is now reachable from `RefundRequested` and
     `PaidAfterExpiry`.
- Live end-to-end run (InMemory + mock Ticketing on :5003): create → signed sandbox URL
  → IPN `rspCode 00` + order `Paid` → refund request → admin approve → order `Refunded`;
  tampered-amount IPN rejected 400 with a valid HMAC over the tampered payload (the
  amount cross-check catches forged amounts beyond the signature).
- NetArchTest boundary rules: Domain depends only on SharedKernel; Contracts are pure
  DTOs; Application never references Infrastructure or Api.

## Known limitations (honest)

- No Docker on this machine: Testcontainers tests are verified by compile + skip probe;
  they must run in CI with Docker.
- The React "order management / refund approval" screens are TV5's deliverable; TV2
  ships the backing APIs (FR-13, FR-22, admin refunds).
- Real VNPay sandbox credentials (TmnCode/HashSecret) must be configured in appsettings
  or environment before pointing at the real gateway; placeholders ship by default.
  Missing config now fails startup (validated in VnPayPaymentService).
- The Ticketing HTTP routes in `HttpTicketingModuleApi` are a consumer-driven contract;
  when TV4 ships Tickets.Contracts, the adapter is the only file that may need mapping.
- TV1's central outbox worker (FOR UPDATE SKIP LOCKED) is not part of this delivery;
  the module-local delivery service covers local development (enable only one of them)
  and the table/format are ready for the central worker.
- `TicketIssuedIntegrationEvent` now carries `TotalAmount` and `Items` for the email
  invoice. Request to TV4: populate these fields when publishing TicketIssued; older
  payloads without pricing still render (the invoice section is simply omitted).

## Authorization contract for TV1's host (JWT middleware + RBAC)

The modules ship no auth of their own — per the assignment, TV1's Identity module and
host middleware enforce JWT + roles. When wiring the modules into the host, apply:

| Endpoint | Required policy |
| --- | --- |
| `POST /api/orders` | Authenticated Customer (order CustomerId must match the token subject) |
| `GET /api/orders/customer/{customerId}` | Authenticated Customer, subject == customerId (blocks IDOR) |
| `GET /api/orders/{id}` | Authenticated Customer owning the order, or Staff/Admin |
| `POST /api/orders/{id}/refund` | Authenticated Customer owning the order |
| `GET /api/orders/refunds` | Admin |
| `POST /api/orders/refunds/{id}/process` | Admin |
| `GET /api/orders/events/{eventId}/attendees` | Organizer owning the event, or Staff |
| `POST /api/payments/vnpay/ipn` | Anonymous by design (gateway callback; HMAC signature is the auth) |
| `GET /api/payments/vnpay/return` | Anonymous by design (browser redirect; signature verified) |
| `POST /api/notifications/send` | Internal service only (service token / network-restricted) |
| `POST /api/integration-events/ticket-issued` | Internal modules only (service token; Inbox dedupes by EventId) |
| `GET /api/notifications/order/{orderId}` | Authenticated Customer owning the order, or Admin |
