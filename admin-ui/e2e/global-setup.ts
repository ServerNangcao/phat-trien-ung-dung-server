import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import http from "node:http";

/**
 * Seeds demo data through the real API flow before the specs run:
 * one paid order, one pending order, one paid order with a refund request,
 * and tickets for the paid + pending orders (the pending ticket must be
 * filtered out of the attendee list by the paid-orders join).
 */
function call(port: number, method: string, apiPath: string, body?: unknown): Promise<{ status: number; body: any }> {
  return new Promise((resolve, reject) => {
    const req = http.request(
      { host: "localhost", port, path: apiPath, method, headers: { "Content-Type": "application/json" } },
      (res) => {
        let data = "";
        res.on("data", (chunk) => (data += chunk));
        res.on("end", () => resolve({ status: res.statusCode ?? 0, body: data ? JSON.parse(data) : null }));
      },
    );
    req.on("error", reject);
    if (body !== undefined) req.write(JSON.stringify(body));
    req.end();
  });
}

function sign(queryString: string): string {
  return crypto.createHmac("sha512", "DEMOHASHSECRET").update(queryString).digest("hex");
}

async function pay(orderId: string, total: number): Promise<void> {
  const entries: Record<string, string> = {
    vnp_Version: "2.1.0",
    vnp_Command: "pay",
    vnp_TmnCode: "DEMODTMB",
    vnp_Amount: String(Math.round(total * 100)),
    vnp_TxnRef: orderId,
    vnp_OrderInfo: "e2e",
    vnp_OrderType: "other",
    vnp_CurrencyCode: "VND",
    vnp_BankCode: "NCB",
    vnp_PayDate: "20261105090000",
    vnp_ResponseCode: "00",
    vnp_TransactionStatus: "00",
    vnp_TransactionNo: "1",
  };
  const query = Object.entries(entries)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");
  const res = await call(5001, "POST", `/api/payments/vnpay/ipn?${query}&vnp_SecureHash=${sign(query)}`);
  if (res.status !== 200) throw new Error(`IPN failed: ${res.status} ${JSON.stringify(res.body)}`);
}

export default async function globalSetup(): Promise<void> {
  const customerId = "99999999-9999-9999-9999-999999999999";
  const eventId = "88888888-8888-8888-8888-888888888888";
  const ticketTypeId = "77777777-7777-7777-7777-777777777777";

  const paid = await call(5001, "POST", "/api/orders", {
    customerId,
    reservationId: crypto.randomUUID(),
  });
  await pay(paid.body.orderId, paid.body.totalAmount);

  const pending = await call(5001, "POST", "/api/orders", {
    customerId,
    reservationId: crypto.randomUUID(),
  });

  const refundRequested = await call(5001, "POST", "/api/orders", {
    customerId,
    reservationId: crypto.randomUUID(),
  });
  await pay(refundRequested.body.orderId, refundRequested.body.totalAmount);
  await call(5001, "POST", `/api/orders/${refundRequested.body.orderId}/refund`, {
    reason: "E2E: cannot attend",
  });

  const now = new Date().toISOString();
  await call(5003, "POST", "/__set-tickets", [
    {
      ticketId: crypto.randomUUID(),
      orderId: paid.body.orderId,
      customerId,
      ticketTypeId,
      ticketTypeName: "VIP",
      status: "Issued",
      issuedAtUtc: now,
    },
    {
      ticketId: crypto.randomUUID(),
      orderId: pending.body.orderId,
      customerId,
      ticketTypeId,
      ticketTypeName: "VIP",
      status: "Issued",
      issuedAtUtc: now,
    },
  ]);

  const seedInfo = {
    customerId,
    eventId,
    paidOrderId: paid.body.orderId as string,
    pendingOrderId: pending.body.orderId as string,
    refundRequestedOrderId: refundRequested.body.orderId as string,
  };
  fs.writeFileSync(path.join(process.cwd(), "e2e", ".seed.json"), JSON.stringify(seedInfo, null, 2));
  console.log("e2e seed:", seedInfo);
}
