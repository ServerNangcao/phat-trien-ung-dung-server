import { useMemo } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowUpRight, DollarSign, Layers, TicketCheck, TrendingUp } from "lucide-react";
import { formatVnd } from "../../api/platformDemo";
import { ticketingApi } from "../../api/service";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { OrganizerLayout } from "./OrganizerLayout";

const STATUS_LABEL: Record<string, string> = {
  Published: "Đang mở bán",
  OnSale: "Đang mở bán",
  Pending: "Chờ duyệt",
  PendingApproval: "Chờ duyệt",
  Draft: "Bản nháp",
  Rejected: "Bị từ chối",
  Ended: "Đã kết thúc",
  Cancelled: "Đã hủy",
};

export function OrganizerAnalyticsPage() {
  const {
    data: events = [],
    isLoading: isLoadingEvents,
    isError: isEventsError,
  } = useQuery({
    queryKey: ["organizer", "events"],
    queryFn: ticketingApi.organizer.getMyEvents,
  });

  const {
    data: orders = [],
    isLoading: isLoadingOrders,
    isError: isOrdersError,
  } = useQuery({
    queryKey: ["organizer", "orders"],
    queryFn: () => ticketingApi.organizer.getOrders(),
  });

  const stats = useMemo(() => {
    let tierRevenue = 0;
    let totalSold = 0;
    let totalCapacity = 0;
    let totalReserved = 0;

    for (const ev of events) {
      for (const tier of ev.tiers) {
        tierRevenue += tier.sold * tier.price;
        totalSold += tier.sold;
        totalCapacity += tier.total;
        totalReserved += tier.reserved;
      }
    }

    const paidOrders = orders.filter((o) => o.paymentStatus === "Paid");
    const orderRevenue = paidOrders.reduce((sum, o) => sum + o.totalAmount, 0);
    const totalRevenue = orderRevenue > 0 ? orderRevenue : tierRevenue;
    const occupancy = totalCapacity > 0 ? Math.round((totalSold / totalCapacity) * 100) : 0;

    return {
      totalRevenue,
      totalSold,
      totalCapacity,
      totalReserved,
      occupancy,
      paidOrdersCount: paidOrders.length,
    };
  }, [events, orders]);

  const isLoading = isLoadingEvents || isLoadingOrders;
  const isError = isEventsError || isOrdersError;

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
      {isError && (
        <div className="mb-6 rounded-2xl border border-rose-200 bg-rose-50 px-5 py-4 text-sm text-rose-700">
          Không thể tải dữ liệu phân tích từ máy chủ. Vui lòng kiểm tra kết nối hoặc đăng nhập lại.
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          {
            label: "Tổng doanh thu",
            value: isLoading ? "..." : formatVnd(stats.totalRevenue),
            hint: "Đã thanh toán thành công",
            icon: DollarSign,
          },
          {
            label: "Vé đã bán",
            value: isLoading ? "..." : `${stats.totalSold} / ${stats.totalCapacity}`,
            hint: `Tỷ lệ lấp đầy ${stats.occupancy}%`,
            icon: TicketCheck,
          },
          {
            label: "Đang giữ chỗ (10p)",
            value: isLoading ? "..." : String(stats.totalReserved),
            hint: "Chờ khách hoàn tất thanh toán",
            icon: Layers,
          },
          {
            label: "Đơn hàng gần đây",
            value: isLoading ? "..." : String(orders.length),
            hint: `${stats.paidOrdersCount} đơn thành công`,
            icon: TrendingUp,
          },
        ].map((kpi) => {
          const Icon = kpi.icon;
          return (
            <div key={kpi.label} className="rounded-2xl border border-slate-200 bg-white p-5 text-[#182226]">
              <div className="flex items-center justify-between text-xs font-semibold text-[#526066]">
                <span>{kpi.label}</span>
                <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-slate-100 text-[#F05B45]">
                  <Icon size={16} />
                </span>
              </div>
              <p className="mt-3 text-2xl font-extrabold tracking-tight text-[#182226]">{kpi.value}</p>
              <p className="mt-1 text-xs text-[#526066]">{kpi.hint}</p>
            </div>
          );
        })}
      </div>

      <div className="mt-8 grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="lg:col-span-7 rounded-2xl border border-slate-200 bg-white p-6 text-[#182226]">
          <h2 className="text-base font-extrabold text-[#182226]">Doanh thu &amp; lấp đầy theo sự kiện</h2>
          {isLoadingEvents ? (
            <p className="mt-5 text-sm text-[#526066]">Đang tải danh sách sự kiện...</p>
          ) : events.length === 0 ? (
            <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-slate-50 p-6 text-center">
              <p className="text-sm font-semibold text-[#182226]">Tài khoản chưa có sự kiện nào</p>
              <p className="mt-1 text-xs text-[#526066]">
                Tạo sự kiện đầu tiên và mở bán hạng vé để bắt đầu ghi nhận doanh thu thật.
              </p>
              <Link to="/organizer/builder" className="btn-primary mt-4 inline-flex">
                Tạo sự kiện đầu tiên <ArrowUpRight size={15} />
              </Link>
            </div>
          ) : (
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
                            ev.status === "Published" || ev.status === "OnSale"
                              ? "success"
                              : ev.status === "Pending" || ev.status === "PendingApproval" || ev.status === "Draft"
                                ? "warning"
                                : "danger"
                          }
                        >
                          {STATUS_LABEL[ev.status] ?? ev.status}
                        </StatusBadge>
                      </div>
                      <span className="font-extrabold text-[#182226]">{formatVnd(rev)}</span>
                    </div>
                    <div className="h-2.5 w-full overflow-hidden rounded-full bg-slate-100">
                      <div
                        className="h-full rounded-full bg-[#F05B45] transition-all duration-300"
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                    <div className="flex justify-between text-xs text-[#526066]">
                      <span>
                        {sold} / {total} vé đã bán
                      </span>
                      <span>{pct}% lấp đầy</span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        <div className="lg:col-span-5 rounded-2xl border border-slate-200 bg-white p-6 text-[#182226]">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-extrabold text-[#182226]">Đơn hàng mới nhất</h2>
            <Link to="/organizer/orders" className="text-xs font-bold text-[#D94735] hover:underline">
              Xem tất cả
            </Link>
          </div>
          {isLoadingOrders ? (
            <p className="mt-5 text-sm text-[#526066]">Đang tải đơn hàng...</p>
          ) : orders.length === 0 ? (
            <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-slate-50 p-6 text-center text-xs text-[#526066]">
              Chưa có đơn hàng nào cho các sự kiện của bạn.
            </div>
          ) : (
            <div className="mt-4 divide-y divide-slate-200">
              {orders.slice(0, 5).map((order) => (
                <div key={order.orderCode} className="flex items-center justify-between gap-3 py-3 text-xs">
                  <div className="min-w-0">
                    <p className="truncate font-bold text-[#182226]">{order.buyerName}</p>
                    <p className="truncate text-[#526066]">
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
          )}
        </div>
      </div>
    </OrganizerLayout>
  );
}
