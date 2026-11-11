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

async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`);
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.message ?? body?.error ?? `Request failed: ${response.status}`);
  }
  return response.json() as Promise<T>;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
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
    get<OrderSummary[]>(`/api/orders/customer/${customerId}`),

  detail: (orderId: string) => get<OrderDetail>(`/api/orders/${orderId}`),

  attendees: (eventId: string) =>
    get<Attendee[]>(`/api/orders/events/${eventId}/attendees`),

  listRefunds: (status?: string) =>
    get<RefundRequest[]>(`/api/orders/refunds${status ? `?status=${status}` : ""}`),

  processRefund: (refundRequestId: string, approve: boolean, adminNote?: string) =>
    post<{ refundRequestId: string }>(`/api/orders/refunds/${refundRequestId}/process`, {
      approve,
      adminNote,
    }),
};
