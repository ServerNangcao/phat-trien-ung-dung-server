import { useCallback, useEffect, useState } from "react";
import { ordersApi, type RefundRequest, type RefundStatus } from "../api";

const filters: Array<{ label: string; value?: RefundStatus }> = [
  { label: "All" },
  { label: "Pending", value: "Pending" },
  { label: "Processed", value: "Processed" },
  { label: "Rejected", value: "Rejected" },
];

export default function RefundsPage() {
  const [filter, setFilter] = useState<string | undefined>("Pending");
  const [refunds, setRefunds] = useState<RefundRequest[]>([]);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRefunds(await ordersApi.listRefunds(filter));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => {
    void load();
  }, [load]);

  async function process(refundRequestId: string, approve: boolean) {
    setError(null);
    setMessage(null);
    try {
      await ordersApi.processRefund(refundRequestId, approve, note || undefined);
      setMessage(`Refund ${refundRequestId.slice(0, 8)}… ${approve ? "approved" : "rejected"}.`);
      setNote("");
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  return (
    <section>
      <h2>Refund reconciliation</h2>
      <p className="hint">
        Admin: review refund requests, then approve (order becomes Refunded) or reject
        (order returns to Paid). The note is stored for audit.
      </p>

      <div className="row">
        {filters.map((f) => (
          <button
            key={f.label}
            className={filter === f.value ? "active" : ""}
            onClick={() => setFilter(f.value)}
          >
            {f.label}
          </button>
        ))}
        <button className="ghost" onClick={() => void load()} disabled={loading}>
          Refresh
        </button>
      </div>

      {error && <p className="error">{error}</p>}
      {message && <p className="success">{message}</p>}

      <div className="row">
        <input
          type="text"
          placeholder="Decision note (stored on the refund request)"
          value={note}
          onChange={(e) => setNote(e.target.value)}
        />
      </div>

      <table>
        <thead>
          <tr>
            <th>Refund</th>
            <th>Order</th>
            <th>Reason</th>
            <th>Status</th>
            <th>Created</th>
            <th>Decision</th>
          </tr>
        </thead>
        <tbody>
          {refunds.map((r) => (
            <tr key={r.id}>
              <td className="mono">{r.id.slice(0, 8)}…</td>
              <td className="mono">{r.orderId.slice(0, 8)}…</td>
              <td>{r.reason}</td>
              <td>
                <span className={`status ${r.status === "Processed" ? "refunded" : r.status === "Rejected" ? "cancelled" : "requested"}`}>
                  {r.status}
                </span>
              </td>
              <td>{new Date(r.createdAt).toLocaleString()}</td>
              <td>
                {r.status === "Pending" ? (
                  <>
                    <button className="approve" onClick={() => void process(r.id, true)}>Approve</button>{" "}
                    <button className="reject" onClick={() => void process(r.id, false)}>Reject</button>
                  </>
                ) : (
                  <span className="hint">
                    {r.processedAt ? new Date(r.processedAt).toLocaleString() : ""}
                    {r.processedNote ? ` — “${r.processedNote}”` : ""}
                  </span>
                )}
              </td>
            </tr>
          ))}
          {refunds.length === 0 && (
            <tr><td colSpan={6}>No refund requests {filter ? `with status ${filter}` : ""}.</td></tr>
          )}
        </tbody>
      </table>
    </section>
  );
}
