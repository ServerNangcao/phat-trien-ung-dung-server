import { useMemo, useState } from "react";
import { Search } from "lucide-react";
import { formatVnd, platformDemoApi } from "../../api/platformDemo";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { OrganizerLayout } from "./OrganizerLayout";

export function OrganizerOrdersPage() {
  const orders = useMemo(() => platformDemoApi.getOrders(), []);
  const [query, setQuery] = useState("");
  const [paymentFilter, setPaymentFilter] = useState<"ALL" | "Paid" | "Pending" | "Refunded">("ALL");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return orders.filter((o) => {
      if (paymentFilter !== "ALL" && o.paymentStatus !== paymentFilter) return false;
      if (!q) return true;
      return (
        o.orderCode.toLowerCase().includes(q) ||
        o.buyerName.toLowerCase().includes(q) ||
        o.buyerEmail.toLowerCase().includes(q) ||
        o.eventTitle.toLowerCase().includes(q)
      );
    });
  }, [orders, query, paymentFilter]);

  return (
    <OrganizerLayout
      title="Đơn hàng & người tham dự"
      subtitle="Tra cứu mã đơn, trạng thái thanh toán VNPay và tình trạng check-in tại cổng."
    >
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white p-4 text-[#182226]">
        <div className="flex flex-1 min-w-[220px] items-center gap-2.5 rounded-xl border border-slate-200 bg-white px-3.5 py-2">
          <Search size={16} className="text-[#526066]" />
          <input
            aria-label="Tìm đơn hàng hoặc người tham dự"
            placeholder="Tìm theo mã đơn, tên hoặc email..."
            className="w-full bg-transparent text-sm text-[#182226] placeholder:text-[#7A8494] outline-none"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </div>
        <div className="flex flex-wrap gap-1.5">
          {(["ALL", "Paid", "Pending", "Refunded"] as const).map((status) => (
            <button
              key={status}
              type="button"
              onClick={() => setPaymentFilter(status)}
              className={`rounded-xl px-3 py-1.5 text-xs font-semibold cursor-pointer transition-colors ${
                paymentFilter === status
                  ? "bg-[#F05B45] text-white"
                  : "border border-slate-200 bg-white text-[#475458] hover:text-[#182226]"
              }`}
            >
              {status === "ALL" ? "Tất cả" : status}
            </button>
          ))}
        </div>
      </div>

      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white text-[#182226]">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs font-bold uppercase tracking-wider text-[#526066]">
            <tr>
              <th className="px-5 py-3.5">Mã đơn</th>
              <th className="px-5 py-3.5">Người tham dự</th>
              <th className="px-5 py-3.5">Sự kiện &amp; Hạng vé</th>
              <th className="px-5 py-3.5">Tổng tiền</th>
              <th className="px-5 py-3.5">Thanh toán</th>
              <th className="px-5 py-3.5">Check-in</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 bg-white">
            {filtered.map((order) => (
              <tr key={order.orderCode} className="hover:bg-slate-50">
                <td className="px-5 py-4">
                  <p className="font-mono text-xs font-bold text-[#182226]">{order.orderCode}</p>
                  <p className="text-[11px] text-[#526066]">{order.createdAt}</p>
                </td>
                <td className="px-5 py-4">
                  <p className="font-bold text-[#182226]">{order.buyerName}</p>
                  <p className="text-xs text-[#526066]">{order.buyerEmail}</p>
                </td>
                <td className="px-5 py-4">
                  <p className="font-semibold text-[#182226]">{order.eventTitle}</p>
                  <p className="text-xs text-[#526066]">
                    {order.tierName} × {order.quantity}
                  </p>
                </td>
                <td className="px-5 py-4 font-bold text-[#182226]">{formatVnd(order.totalAmount)}</td>
                <td className="px-5 py-4">
                  <StatusBadge
                    tone={
                      order.paymentStatus === "Paid"
                        ? "success"
                        : order.paymentStatus === "Pending"
                          ? "warning"
                          : "neutral"
                    }
                  >
                    {order.paymentStatus}
                  </StatusBadge>
                </td>
                <td className="px-5 py-4">
                  <StatusBadge tone={order.checkInStatus === "CheckedIn" ? "success" : "neutral"}>
                    {order.checkInStatus === "CheckedIn" ? "Đã vào cổng" : "Chưa quét"}
                  </StatusBadge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </OrganizerLayout>
  );
}
