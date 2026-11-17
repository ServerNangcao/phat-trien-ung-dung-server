import { useState } from "react";
import OrdersPage from "./pages/OrdersPage";
import AttendeesPage from "./pages/AttendeesPage";
import RefundsPage from "./pages/RefundsPage";

type Tab = "orders" | "attendees" | "refunds";

const tabs: Array<{ id: Tab; label: string }> = [
  { id: "orders", label: "Orders" },
  { id: "attendees", label: "Attendees" },
  { id: "refunds", label: "Refunds" },
];

export default function App() {
  const [tab, setTab] = useState<Tab>("orders");

  return (
    <main className="app">
      <header>
        <h1>Ticketing — TV2 back office</h1>
        <nav>
          {tabs.map((t) => (
            <button key={t.id} className={tab === t.id ? "active" : ""} onClick={() => setTab(t.id)}>
              {t.label}
            </button>
          ))}
        </nav>
      </header>
      {tab === "orders" && <OrdersPage />}
      {tab === "attendees" && <AttendeesPage />}
      {tab === "refunds" && <RefundsPage />}
      <footer className="hint">
        Talks to Orders.Api (VITE_ORDERS_API, default http://localhost:5001). JWT policies
        are enforced by TV1's host middleware in production.
      </footer>
    </main>
  );
}
