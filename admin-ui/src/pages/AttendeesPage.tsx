import { useState } from "react";
import { ordersApi, type Attendee } from "../api";

export default function AttendeesPage() {
  const [eventId, setEventId] = useState("");
  const [attendees, setAttendees] = useState<Attendee[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function loadAttendees() {
    setLoading(true);
    setError(null);
    try {
      setAttendees(await ordersApi.attendees(eventId.trim()));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setAttendees(null);
    } finally {
      setLoading(false);
    }
  }

  return (
    <section>
      <h2>Attendee list</h2>
      <p className="hint">
        FR-22: tickets issued for the event, joined with Orders so only paid orders appear.
      </p>

      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault();
          void loadAttendees();
        }}
      >
        <input
          type="text"
          placeholder="Event ID (GUID)"
          value={eventId}
          onChange={(e) => setEventId(e.target.value)}
          required
        />
        <button type="submit" disabled={loading}>
          {loading ? "Loading…" : "Load attendees"}
        </button>
      </form>

      {error && <p className="error">{error}</p>}

      {attendees && (
        <table>
          <thead>
            <tr>
              <th>Ticket</th>
              <th>Type</th>
              <th>Customer</th>
              <th>Order</th>
              <th>Status</th>
              <th>Issued</th>
            </tr>
          </thead>
          <tbody>
            {attendees.map((a) => (
              <tr key={a.ticketId}>
                <td className="mono">{a.ticketId.slice(0, 8)}…</td>
                <td>{a.ticketType}</td>
                <td className="mono">{a.customerId.slice(0, 8)}…</td>
                <td className="mono">{a.orderId.slice(0, 8)}…</td>
                <td><span className="status paid">{a.status}</span></td>
                <td>{new Date(a.issuedAt).toLocaleString()}</td>
              </tr>
            ))}
            {attendees.length === 0 && (
              <tr><td colSpan={6}>No attendees yet for this event.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  );
}
