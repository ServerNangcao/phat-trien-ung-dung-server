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

Tests: `dotnet test` — 78 tests (75 pass; 3 Testcontainers Postgres tests skip when
Docker is absent and run in CI with Docker).

UI E2E: `cd backend/admin-ui && npm run test:e2e` — Playwright boots the full stack
(Orders.Api on InMemory, mock Ticketing, built UI preview), seeds data through the real
API flow, and verifies the three screens end to end (order search + detail, attendee
paid-orders join, refund approve with audit note → order Refunded). Requires the
Playwright Chromium once (`npx playwright install chromium`).

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

## Back-office screens (TV2 auxiliary frontend)

`backend/admin-ui` — Vite + React 19 + TypeScript, plain CSS (TV5 can restyle or absorb
the pages into the main portal).

```bash
cd backend/admin-ui
npm install
npm run dev        # http://localhost:5173 (CORS-allowed origin)
npm run build      # type-checks and bundles to dist/
npm run preview    # serves the build at http://localhost:4173 (also CORS-allowed)
```

Pages:
- **Orders** (FR-13): customer lookup, order list with status chips, detail with items.
- **Attendees** (FR-22): per-event attendee list; the backend joins Ticketing's issued
  tickets with paid orders only, so a pending order's ticket is filtered out.
- **Refunds**: status filters, approve/reject with an audit note (persisted on the
  refund request and shown after processing); approving moves the order to Refunded.

The API base URL is `VITE_ORDERS_API` (default `http://localhost:5001`). Allowed browser
origins are configured in Orders.Api `Cors:AllowedOrigins`. JWT authorization remains
TV1's host concern (policies listed above).

## Known limitations (honest)

- No Docker on this machine: Testcontainers tests are verified by compile + skip probe;
  they must run in CI with Docker.
- The React "order management / refund approval" screens are delivered in
  `backend/admin-ui` (TV2's auxiliary back office); TV5 may absorb them into the main
  portal restyled with the platform's design system.
- Real VNPay sandbox credentials (TmnCode/HashSecret) must be configured in appsettings
  or environment before pointing at the real gateway; placeholders ship by default.
  Missing config now fails startup (validated in VnPayPaymentService).
  **Setup path**: `bash scripts/vnpay-sandbox-setup.sh` — an interactive wizard that
  walks through registering at sandbox.vnpayment.vn/devreg, captures TmnCode +
  HashSecret into `dotnet user-secrets` (kept out of source), and ends by opening a
  live sandbox payment URL generated with the real credentials. **Portal login note**:
  the merchant admin SPA needs the anti-forgery cookie from a prior GET before the
  submit works; log in with a browser or Playwright, not a plain `curl` POST.
- **VNPay signing — RESOLVED (2026-11-05, live sandbox evidence).** The scheme is
  **HMAC-SHA512 over the sorted `key=value` pairs joined with `&`, with values
  URL-encoded (`space as '+'`, `URLSearchParams` rules)**. This is what the code
  already does (`VnPayPaymentService.EncodeVnpayValue` / `SignData`,
  `VnPayTestHelper.Encode`); no code change was needed.

  Evidence, in order of strength:
  1. The official docs' own example URL
     (`sandbox.vnpayment.vn/paymentv2/vpcpay.html?...&vnp_TmnCode=DEMOV210&vnp_SecureHash=3e0d61…`)
     returns **code 15 (expired waiting time)**, not code 70 — i.e. the gateway
     *verified the signature* and only rejected the timestamp. Reproducing that
     signature required SHA512 + URL-encoded values; raw-value SHA512 and SHA256
     variants all fail.
  2. The official NodeJS sample signs with `querystring.stringify(params, { encode: false })`
     (raw values) but *sends* the encoded transport; the PHP sample URL-encodes both.
     The gateway's own error page is dynamic: `code=70` renders **"Invalid signature"**
     while `code=15`/`code=99` render a generic message, so the gateway is genuinely
     checking the checksum.
  3. The `merchant_webapi` querydr endpoint is unreliable (returns `99 Unexpected error`
     for valid and invalid signatures alike), so it was not used as an oracle.
  4. The portal (JS-rendered SPA) was read with Playwright after cracking the login
     (the login form needs the anti-forgery cookie established first, then submit via
     Enter — clicking the button is intercepted by a modal). Terminal **IAS2CKBJ**
     ("Ticketing System") exists and its `hashType` is **HMACSHA512**, matching the
     code. The HashSecret is not displayed in the UI (only `ipnUrl`, `callType`,
     `hashType`, `configData` are editable).
  5. **BLOCKER: the HashSecret from the devreg email does not validate.** Every
     signing variant (raw/encoded/%20 × SHA256/SHA512, ±SecureHashType, different
     IPs, NCB bank code, no locale) returns code 70. Community reports and the
     HikaShop plugin docs confirm code 70 = "the hash secret does not match the
     terminal code". The terminal exists with the right algorithm, so the secret
     itself is wrong. Fix: regenerate the secret in the portal or request a fresh
     TmnCode/HashSecret pair from sandbox.vnpayment.vn/devreg. Until then the live
     sandbox payment page cannot be opened — the signing *code* is correct and ready.
- **VNPay IPN contract — verified against the reference library**: HTTP 200 with JSON
  `{RspCode, Message}`; `00` success, `01` order not found, `04` invalid amount,
  `97` invalid checksum, `99` other. The implementation matches this table.
- **VNPay refund API is restricted in sandbox** (reference-library docs): the gateway
  refund call is deliberately left to the payments side via
  `RefundProcessedIntegrationEvent`, so the sandbox flow completes without it.
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
