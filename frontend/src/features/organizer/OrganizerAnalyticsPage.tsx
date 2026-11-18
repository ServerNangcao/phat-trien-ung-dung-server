import { useMemo } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, DollarSign, Layers, TicketCheck, TrendingUp } from "lucide-react";
import { formatVnd, platformDemoApi } from "../../api/platformDemo";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { OrganizerLayout } from "./OrganizerLayout";

export function OrganizerAnalyticsPage() {
  const events = useMemo(() => platformDemoApi.getManagedEvents(), []);
  const orders = useMemo(() => platformDemoApi.getOrders(), []);

  const stats = useMemo(() => {
    let totalRevenue = 0;
    let totalSold = 0;
    let totalCapacity = 0;
    let totalReserved = 0;
    for (const ev of events) {
      for (const tier of ev.tiers) {
        totalRevenue += tier.sold * tier.price;
        totalSold += tier.sold;
        totalCapacity += tier.total;
        totalReserved += tier.reserved;
      }
    }
    const occupancy = totalCapacity > 0 ? Math.round((totalSold / totalCapacity) * 100) : 0;
    return { totalRevenue, totalSold, totalCapacity, totalReserved, occupancy };
  }, [events]);

  return (
    <OrganizerLayout
      title="Doanh thu & phân tích"
      subtitle="Theo dõi hiệu suất bán vé, tỷ lệ lấp đầy và lượt giữ chỗ 10 phút theo thời gian thực."
      action={
        <Link to="/organizer/builder" className="btn-primary">
          Tạo sự kiện mới <ArrowUpRight size={16} />
        </Link>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          {
            label: "Tổng doanh thu",
            value: formatVnd(stats.totalRevenue),
            hint: "Đã thanh toán qua VNPay",
            icon: DollarSign,
          },
          {
            label: "Vé đã bán",
            value: `${stats.totalSold} / ${stats.totalCapacity}`,
            hint: `Tỷ lệ lấp đầy ${stats.occupancy}%`,
            icon: TicketCheck,
          },
          {
            label: "Đang giữ chỗ (10p)",
            value: String(stats.totalReserved),
            hint: "Chờ khách hoàn tất thanh toán",
            icon: Layers,
          },
          {
            label: "Đơn hàng gần đây",
            value: String(orders.length),
            hint: `${orders.filter((o) => o.paymentStatus === "Paid").length} đơn thành công`,
            icon: TrendingUp,
          },
        ].map((kpi) => {
          const Icon = kpi.icon;
          return (
            <div key={kpi.label} className="rounded-2xl border border-[#E6E4DD] bg-white p-5">
              <div className="flex items-center justify-between text-xs font-semibold text-[#647075]">
                <span>{kpi.label}</span>
                <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-[#F3ECE2] text-[#F05A3E]">
                  <Icon size={16} />
                </span>
              </div>
              <p className="mt-3 text-2xl font-extrabold tracking-tight text-[#182226]">{kpi.value}</p>
              <p className="mt-1 text-xs text-[#647075]">{kpi.hint}</p>
            </div>
          );
        })}
      </div>

      <div className="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="lg:col-span-7 rounded-2xl border border-[#E6E4DD] bg-white p-6">
          <h2 className="text-base font-extrabold text-[#182226]">Doanh thu &amp; lấp đầy theo sự kiện</h2>
          <div className="mt-5 space-y-5">
            {events.map((ev) => {
              const sold = ev.tiers.reduce((s, t) => s + t.sold, 0);
              const total = ev.tiers.reduce((s, t) => s + t.total, 0);
              const rev = ev.tiers.reduce((s, t) => s + t.sold * t.price, 0);
              const pct = total > 0 ? Math.round((sold / total) * 100) : 0;
              return (
                <div key={ev.id} className="space-y-2">
                  <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
                    <div className="flex items-center gap-2 font-bold text-[#182226]">
                      <span>{ev.title}</span>
                      <StatusBadge
                        tone={
                          ev.status === "Published"
                            ? "success"
                            : ev.status === "Pending"
                              ? "warning"
                              : "danger"
                        }
                      >
                        {ev.status}
                      </StatusBadge>
                    </div>
                    <span className="font-extrabold text-[#182226]">{formatVnd(rev)}</span>
                  </div>
                  <div className="h-2.5 w-full overflow-hidden rounded-full bg-[#EEECE6]">
                    <div
                      className="h-full rounded-full bg-[#F05A3E] transition-all duration-300"
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                  <div className="flex justify-between text-xs text-[#647075]">
                    <span>{sold} / {total} vé đã bán</span>
                    <span>{pct}% lấp đầy</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="lg:col-span-5 rounded-2xl border border-[#E6E4DD] bg-white p-6">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-extrabold text-[#182226]">Đơn hàng mới nhất</h2>
            <Link to="/organizer/orders" className="text-xs font-bold text-[#F05A3E] hover:underline">
              Xem tất cả
            </Link>
          </div>
          <div className="mt-4 divide-y divide-[#EEECE6]">
            {orders.slice(0, 4).map((order) => (
              <div key={order.orderCode} className="flex items-center justify-between gap-3 py-3 text-xs">
                <div className="min-w-0">
                  <p className="truncate font-bold text-[#182226]">{order.buyerName}</p>
                  <p className="truncate text-[#647075]">
                    {order.orderCode} · {order.tierName} × {order.quantity}
                  </p>
                </div>
                <div className="text-right shrink-0">
                  <p className="font-bold text-[#182226]">{formatVnd(order.totalAmount)}</p>
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
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </OrganizerLayout>
  );
}
