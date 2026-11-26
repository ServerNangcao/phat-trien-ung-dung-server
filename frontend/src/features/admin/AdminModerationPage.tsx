import { useMemo, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BadgeCheck, Ban, Calendar, Check, MapPin, Ticket, X } from "lucide-react";
import { formatVnd } from "../../api/platformDemo";
import { ticketingApi } from "../../api/service";
import type { ManagedEvent } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { AdminLayout } from "./AdminLayout";

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

type StatusFilter = "ALL" | "Pending" | "Published" | "Rejected" | "Cancelled";

function canCancelEvent(status: string): boolean {
  return status !== "Cancelled" && status !== "Ended" && status !== "Rejected";
}

export function AdminModerationPage() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("ALL");
  const [rejectTarget, setRejectTarget] = useState<ManagedEvent | null>(null);
  const [rejectReason, setRejectReason] = useState(
    "Vui lòng bổ sung thông tin chi tiết và giấy phép tổ chức sự kiện.",
  );
  const [cancelTarget, setCancelTarget] = useState<ManagedEvent | null>(null);
  const [cancelReason, setCancelReason] = useState(
    "Sự kiện đã bị hủy bởi quản trị viên.",
  );
  const [actionError, setActionError] = useState<string | null>(null);

  const { data: events = [], isLoading, isError } = useQuery({
    queryKey: ["admin", "events"],
    queryFn: () => ticketingApi.adminEvents.list(),
  });

  const filteredEvents = useMemo(() => {
    if (statusFilter === "ALL") return events;
    if (statusFilter === "Pending") {
      return events.filter((ev) => ev.status === "Pending" || ev.status === "PendingApproval");
    }
    if (statusFilter === "Published") {
      return events.filter((ev) => ev.status === "Published" || ev.status === "OnSale");
    }
    return events.filter((ev) => ev.status === statusFilter);
  }, [events, statusFilter]);

  const invalidateEventQueries = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["admin", "events"] }),
      queryClient.invalidateQueries({ queryKey: ["organizer", "events"] }),
      queryClient.invalidateQueries({ queryKey: ["events"] }),
      queryClient.invalidateQueries({ queryKey: ["event"] }),
    ]);

  const approveMutation = useMutation({
    mutationFn: (eventId: string) => ticketingApi.adminEvents.approve(eventId),
    onSuccess: async () => {
      setActionError(null);
      await invalidateEventQueries();
    },
    onError: (err: unknown) => {
      const msg =
        (err as { response?: { data?: { detail?: string; error?: string } } })?.response?.data?.detail ??
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        "Không thể duyệt sự kiện này (chỉ sự kiện ở trạng thái Chờ duyệt và đã có hạng vé mới có thể duyệt).";
      setActionError(msg);
    },
  });

  const rejectMutation = useMutation({
    mutationFn: ({ eventId, reason }: { eventId: string; reason: string }) =>
      ticketingApi.adminEvents.reject(eventId, reason),
    onSuccess: async () => {
      setActionError(null);
      setRejectTarget(null);
      await invalidateEventQueries();
    },
    onError: () => {
      setActionError("Không thể từ chối sự kiện này.");
    },
  });

  const cancelMutation = useMutation({
    mutationFn: ({ eventId, reason }: { eventId: string; reason?: string }) =>
      ticketingApi.adminEvents.cancel(eventId, reason),
    onSuccess: async () => {
      setActionError(null);
      setCancelTarget(null);
      await invalidateEventQueries();
    },
    onError: () => {
      setActionError("Không thể hủy sự kiện này.");
    },
  });

  const handleRejectSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!rejectTarget) return;
    rejectMutation.mutate({ eventId: rejectTarget.id, reason: rejectReason.trim() });
  };

  const handleCancelSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!cancelTarget) return;
    cancelMutation.mutate({ eventId: cancelTarget.id, reason: cancelReason.trim() || undefined });
  };

  return (
    <AdminLayout
      title="Kiểm duyệt sự kiện"
      subtitle="Xem xét hồ sơ sự kiện do Ban tổ chức gửi lên trước khi phát hành công khai."
    >
      <div className="mb-5 flex flex-wrap gap-2">
        {[
          { id: "ALL" as const, label: "Tất cả" },
          { id: "Pending" as const, label: "Chờ duyệt" },
          { id: "Published" as const, label: "Đang mở bán" },
          { id: "Rejected" as const, label: "Bị từ chối" },
          { id: "Cancelled" as const, label: "Đã hủy" },
        ].map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setStatusFilter(tab.id)}
            className={`rounded-xl px-3.5 py-2 text-xs font-bold cursor-pointer transition-colors ${
              statusFilter === tab.id
                ? "bg-[#F05B45] text-white"
                : "border border-slate-200 bg-white text-[#475458] hover:text-[#182226]"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {(isError || actionError) && (
        <div className="mb-5 rounded-2xl border border-rose-200 bg-rose-50 px-5 py-4 text-sm text-rose-700">
          {actionError ?? "Không thể tải danh sách sự kiện kiểm duyệt từ máy chủ."}
        </div>
      )}

      {isLoading ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-[#526066]">
          Đang tải danh sách sự kiện...
        </div>
      ) : filteredEvents.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center text-sm text-[#526066]">
          Không có sự kiện nào trong mục này.
        </div>
      ) : (
        <div className="space-y-4">
          {filteredEvents.map((ev) => (
            <article
              key={ev.id}
              className="flex flex-col justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-6 text-[#182226] md:flex-row md:items-center"
            >
              <div className="space-y-2">
                <div className="flex flex-wrap items-center gap-2.5">
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
                  <span className="text-xs font-bold uppercase tracking-wider text-[#526066]">
                    {ev.category}
                  </span>
                  <span className="inline-flex items-center gap-1 text-xs font-semibold text-[#526066]">
                    {ev.verified && <BadgeCheck size={14} className="text-emerald-600" />}
                    {ev.organizer}
                  </span>
                </div>

                <h2 className="text-lg font-extrabold text-[#182226]">{ev.title}</h2>
                {ev.description && <p className="text-sm text-[#526066] line-clamp-2">{ev.description}</p>}

                <div className="flex flex-wrap gap-4 pt-1 text-xs text-[#526066]">
                  <span className="inline-flex items-center gap-1.5">
                    <Calendar size={14} /> {ev.date}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin size={14} /> {ev.venue}
                    {ev.city ? `, ${ev.city}` : ""}
                  </span>
                </div>

                <div className="flex flex-wrap items-center gap-2 pt-1">
                  <span className="inline-flex items-center gap-1 text-xs font-bold text-[#526066]">
                    <Ticket size={14} className="text-[#F05B45]" /> Hạng vé &amp; giá vé:
                  </span>
                  {ev.tiers.length > 0 ? (
                    ev.tiers.map((tier) => (
                      <span
                        key={tier.id}
                        className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-[#182226]"
                      >
                        <span>{tier.name}</span>
                        <span className="text-[#526066]">·</span>
                        <span className="text-[#D94735]">
                          {tier.price === 0 ? "Miễn phí" : formatVnd(tier.price)}
                        </span>
                        <span className="text-[#526066]">·</span>
                        <span className="text-[#526066]">
                          {tier.total.toLocaleString("vi-VN")} vé (tối đa {tier.maxPerOrder}/đơn)
                        </span>
                      </span>
                    ))
                  ) : (
                    <StatusBadge tone="warning">Chưa có hạng vé &amp; giá vé</StatusBadge>
                  )}
                </div>

                {(ev.status === "Rejected" || ev.status === "Cancelled") && ev.rejectReason && (
                  <p className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2 text-xs text-rose-700">
                    <b>{ev.status === "Cancelled" ? "Lý do hủy:" : "Lý do từ chối:"}</b> {ev.rejectReason}
                  </p>
                )}
              </div>

              <div className="flex shrink-0 flex-wrap gap-2">
                {(ev.status === "Pending" || ev.status === "PendingApproval") && (
                  <>
                    <button
                      type="button"
                      disabled={approveMutation.isPending}
                      onClick={() => approveMutation.mutate(ev.id)}
                      className="btn-primary !py-2.5 !px-4"
                    >
                      <Check size={16} /> Duyệt phát hành
                    </button>
                    <button
                      type="button"
                      disabled={rejectMutation.isPending}
                      onClick={() => setRejectTarget(ev)}
                      className="btn-outline !py-2.5 !px-4 hover:!border-rose-400 hover:!text-rose-700"
                    >
                      <X size={16} /> Từ chối
                    </button>
                  </>
                )}
                {canCancelEvent(ev.status) && (
                  <button
                    type="button"
                    disabled={cancelMutation.isPending}
                    onClick={() => setCancelTarget(ev)}
                    className="btn-outline !py-2.5 !px-4 !border-rose-200 !text-rose-700 hover:!border-rose-500 hover:!bg-rose-50"
                  >
                    <Ban size={16} /> Hủy sự kiện
                  </button>
                )}
              </div>
            </article>
          ))}
        </div>
      )}

      {cancelTarget && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="cancel-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"
        >
          <form
            onSubmit={handleCancelSubmit}
            className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 text-[#182226] shadow-2xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <h2 id="cancel-modal-title" className="text-lg font-extrabold text-[#182226]">
                Hủy sự kiện
              </h2>
              <button
                type="button"
                aria-label="Đóng hộp thoại"
                onClick={() => setCancelTarget(null)}
                className="rounded-lg p-1.5 text-[#526066] hover:bg-black/5 cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>
            <p className="text-xs text-[#526066]">
              Sự kiện: <b className="text-[#182226]">{cancelTarget.title}</b>
            </p>
            <p className="rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-2.5 text-xs text-amber-800">
              Khi hủy, toàn bộ sự kiện sẽ bị gỡ khỏi danh sách sự kiện công khai và ngừng tiếp nhận đặt vé.
            </p>
            <div>
              <label htmlFor="cancel-reason" className="block text-xs font-semibold text-[#526066] mb-1.5">
                Lý do hủy sự kiện
              </label>
              <textarea
                id="cancel-reason"
                rows={4}
                required
                className="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none focus:border-[#F05B45] focus-visible:ring-2 focus-visible:ring-[#F05B45]/25"
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
              />
            </div>
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setCancelTarget(null)} className="btn-outline">
                Đóng
              </button>
              <button
                type="submit"
                disabled={cancelMutation.isPending}
                className="btn-primary !bg-rose-600 hover:!bg-rose-700"
              >
                {cancelMutation.isPending ? "Đang hủy..." : "Xác nhận hủy sự kiện"}
              </button>
            </div>
          </form>
        </div>
      )}

      {rejectTarget && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="reject-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"
        >
          <form
            onSubmit={handleRejectSubmit}
            className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 text-[#182226] shadow-2xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <h2 id="reject-modal-title" className="text-lg font-extrabold text-[#182226]">
                Từ chối sự kiện
              </h2>
              <button
                type="button"
                aria-label="Đóng hộp thoại"
                onClick={() => setRejectTarget(null)}
                className="rounded-lg p-1.5 text-[#526066] hover:bg-black/5 cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>
            <p className="text-xs text-[#526066]">
              Sự kiện: <b className="text-[#182226]">{rejectTarget.title}</b>
            </p>
            <div>
              <label htmlFor="reject-reason" className="block text-xs font-semibold text-[#526066] mb-1.5">
                Lý do từ chối (gửi tới Ban tổ chức)
              </label>
              <textarea
                id="reject-reason"
                rows={4}
                required
                className="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none focus:border-[#F05B45] focus-visible:ring-2 focus-visible:ring-[#F05B45]/25"
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
              />
            </div>
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setRejectTarget(null)} className="btn-outline">
                Hủy
              </button>
              <button type="submit" disabled={rejectMutation.isPending} className="btn-primary">
                {rejectMutation.isPending ? "Đang xử lý..." : "Xác nhận từ chối"}
              </button>
            </div>
          </form>
        </div>
      )}
    </AdminLayout>
  );
}
