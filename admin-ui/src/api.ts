export type OrderStatus =
  | "Pending"
  | "Paid"
  | "RefundRequested"
  | "Refunded"
  | "Cancelled"
  | "PaidAfterExpiry";

export interface OrderSummary {
  orderId: string;
  reservationId: string;
  totalAmount: number;
  status: OrderStatus;
  createdAt: string;
}

export interface OrderItem {
  ticketTypeId: string;
  quantity: number;
  price: number;
  subtotal: number;
}

export interface OrderDetail extends OrderSummary {
  customerId: string;
  items: OrderItem[];
}

export interface Attendee {
  ticketId: string;
  orderId: string;
  customerId: string;
  ticketTypeId: string;
  ticketType: string;
  status: string;
  issuedAt: string;
}

export type RefundStatus = "Pending" | "Processed" | "Rejected";

export interface RefundRequest {
  id: string;
  orderId: string;
  reason: string;
  status: RefundStatus;
  createdAt: string;
  processedAt: string | null;
  processedNote?: string | null;
}

const API_BASE = import.meta.env.VITE_ORDERS_API ?? "http://localhost:5001";

function getHeaders(): Record<string, string> {
  const headers: Record<string, string> = {};
  const token = typeof localStorage !== "undefined" ? localStorage.getItem("token") : null;
  const bearer = token || (import.meta.env?.VITE_API_TOKEN as string | undefined);
  if (bearer) {
    headers["Authorization"] = `Bearer ${bearer}`;
  }
  return headers;
}

async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: getHeaders(),
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.message ?? body?.error ?? `Request failed: ${response.status}`);
  }
  return response.json() as Promise<T>;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...getHeaders(),
    },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new Error(payload?.message ?? payload?.error ?? `Request failed: ${response.status}`);
  }
  return response.json() as Promise<T>;
}

export const ordersApi = {
  listByCustomer: (customerId: string) =>
    get<OrderSummary[]>(`/api/orders/customer/${encodeURIComponent(customerId)}`),

  detail: (orderId: string) => get<OrderDetail>(`/api/orders/${encodeURIComponent(orderId)}`),

  attendees: (eventId: string) =>
    get<Attendee[]>(`/api/orders/events/${encodeURIComponent(eventId)}/attendees`),

  listRefunds: (status?: string) =>
    get<RefundRequest[]>(`/api/orders/refunds${status ? `?status=${encodeURIComponent(status)}` : ""}`),

  processRefund: (refundRequestId: string, approve: boolean, adminNote?: string) =>
    post<{ refundRequestId: string }>(`/api/orders/refunds/${encodeURIComponent(refundRequestId)}/process`, {
      approve,
      adminNote,
    }),
};
