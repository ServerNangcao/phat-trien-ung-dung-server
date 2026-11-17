import { useState } from "react";
import { ordersApi, type OrderDetail, type OrderSummary, type OrderStatus } from "../api";

const statusClass: Record<OrderStatus, string> = {
  Pending: "status pending",
  Paid: "status paid",
  RefundRequested: "status requested",
  Refunded: "status refunded",
  Cancelled: "status cancelled",
  PaidAfterExpiry: "status late",
};

export default function OrdersPage() {
  const [customerId, setCustomerId] = useState("");
  const [orders, setOrders] = useState<OrderSummary[] | null>(null);
  const [detail, setDetail] = useState<OrderDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function loadOrders() {
    setLoading(true);
    setError(null);
    setDetail(null);
    try {
      setOrders(await ordersApi.listByCustomer(customerId.trim()));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setOrders(null);
    } finally {
      setLoading(false);
    }
  }

  async function loadDetail(orderId: string) {
    setError(null);
    try {
      setDetail(await ordersApi.detail(orderId));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  return (
    <section>
      <h2>Order management</h2>
      <p className="hint">FR-13: look up a customer's orders, inspect items and payment state.</p>

      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault();
          void loadOrders();
        }}
      >
        <input
          type="text"
          placeholder="Customer ID (GUID)"
          value={customerId}
          onChange={(e) => setCustomerId(e.target.value)}
          required
        />
        <button type="submit" disabled={loading}>
          {loading ? "Loading…" : "Search orders"}
        </button>
      </form>

      {error && <p className="error">{error}</p>}

      {orders && (
        <table>
          <thead>
            <tr>
              <th>Order</th>
              <th>Total (VND)</th>
              <th>Status</th>
              <th>Created</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {orders.map((o) => (
              <tr key={o.orderId}>
                <td className="mono">{o.orderId.slice(0, 8)}…</td>
                <td>{o.totalAmount.toLocaleString()}</td>
                <td><span className={statusClass[o.status]}>{o.status}</span></td>
                <td>{new Date(o.createdAt).toLocaleString()}</td>
                <td>
                  <button onClick={() => void loadDetail(o.orderId)}>Detail</button>
                </td>
              </tr>
            ))}
            {orders.length === 0 && (
              <tr><td colSpan={5}>No orders for this customer.</td></tr>
            )}
          </tbody>
        </table>
      )}

      {detail && (
        <div className="detail">
          <h3>Order {detail.orderId}</h3>
          <p>
            <span className={statusClass[detail.status]}>{detail.status}</span>{" "}
            — total <strong>{detail.totalAmount.toLocaleString()} VND</strong> — created{" "}
            {new Date(detail.createdAt).toLocaleString()}
          </p>
          <p className="hint">
            Reservation {detail.reservationId} · Customer {detail.customerId}
          </p>
          <table>
            <thead>
              <tr><th>Ticket type</th><th>Qty</th><th>Price</th><th>Subtotal</th></tr>
            </thead>
            <tbody>
              {detail.items.map((i) => (
                <tr key={i.ticketTypeId}>
                  <td className="mono">{i.ticketTypeId.slice(0, 8)}…</td>
                  <td>{i.quantity}</td>
                  <td>{i.price.toLocaleString()}</td>
                  <td>{i.subtotal.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
