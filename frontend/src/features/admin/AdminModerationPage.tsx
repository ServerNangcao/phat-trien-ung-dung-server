import { useState, type FormEvent } from "react";
import { BadgeCheck, Calendar, Check, MapPin, X } from "lucide-react";
import { platformDemoApi } from "../../api/platformDemo";
import type { ManagedEvent } from "../../api/types";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { AdminLayout } from "./AdminLayout";

export function AdminModerationPage() {
  const [events, setEvents] = useState<ManagedEvent[]>(() => platformDemoApi.getManagedEvents());
  const [rejectTarget, setRejectTarget] = useState<ManagedEvent | null>(null);
  const [rejectReason, setRejectReason] = useState(
    "Vui lòng bổ sung bản scan giấy phép biểu diễn nghệ thuật và sơ đồ thoát hiểm tại địa điểm.",
  );

  const handleApprove = (eventId: string) => {
    setEvents(platformDemoApi.moderateEvent(eventId, "Published"));
  };

  const handleRejectSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!rejectTarget) return;
    setEvents(platformDemoApi.moderateEvent(rejectTarget.id, "Rejected", rejectReason.trim()));
    setRejectTarget(null);
  };

  return (
    <AdminLayout
      title="Kiểm duyệt sự kiện"
      subtitle="Xem xét hồ sơ sự kiện do Ban tổ chức gửi lên trước khi phát hành công khai."
    >
      <div className="space-y-4">
        {events.map((ev) => (
          <article
            key={ev.id}
            className="flex flex-col justify-between gap-4 rounded-2xl border border-[#E6E4DD] bg-white p-6 md:flex-row md:items-center"
          >
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2.5">
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
                <span className="text-xs font-bold uppercase tracking-wider text-[#647075]">
                  {ev.category}
                </span>
                <span className="inline-flex items-center gap-1 text-xs font-semibold text-[#526066]">
                  {ev.verified && <BadgeCheck size={14} className="text-emerald-600" />}
                  {ev.organizer}
                </span>
              </div>

              <h2 className="text-lg font-extrabold text-[#182226]">{ev.title}</h2>
              <p className="text-sm text-[#647075]">{ev.description}</p>

              <div className="flex flex-wrap gap-4 pt-1 text-xs text-[#526066]">
                <span className="inline-flex items-center gap-1.5">
                  <Calendar size={14} /> {ev.date}
                </span>
                <span className="inline-flex items-center gap-1.5">
                  <MapPin size={14} /> {ev.venue}, {ev.city}
                </span>
              </div>

              {ev.status === "Rejected" && ev.rejectReason && (
                <p className="rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-2 text-xs text-rose-700">
                  <b>Lý do từ chối:</b> {ev.rejectReason}
                </p>
              )}
            </div>

            <div className="flex shrink-0 flex-wrap gap-2">
              {ev.status !== "Published" && (
                <button
                  type="button"
                  onClick={() => handleApprove(ev.id)}
                  className="btn-primary !py-2.5 !px-4"
                >
                  <Check size={16} /> Duyệt phát hành
                </button>
              )}
              {ev.status !== "Rejected" && (
                <button
                  type="button"
                  onClick={() => setRejectTarget(ev)}
                  className="btn-outline !py-2.5 !px-4 hover:!border-rose-400 hover:!text-rose-700"
                >
                  <X size={16} /> Từ chối
                </button>
              )}
            </div>
          </article>
        ))}
      </div>

      {rejectTarget && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="reject-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4 backdrop-blur-xs"
        >
          <form
            onSubmit={handleRejectSubmit}
            className="w-full max-w-md rounded-2xl border border-[#E6E4DD] bg-white p-6 shadow-xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <h2 id="reject-modal-title" className="text-lg font-extrabold text-[#182226]">
                Từ chối sự kiện
              </h2>
              <button
                type="button"
                aria-label="Đóng hộp thoại"
                onClick={() => setRejectTarget(null)}
                className="rounded-lg p-1.5 text-[#647075] hover:bg-[#F3ECE2] cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>
            <p className="text-xs text-[#647075]">
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
                className="w-full rounded-xl border border-[#E6E4DD] bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none focus:border-[#F05A3E] focus-visible:ring-2 focus-visible:ring-[#F05A3E]/25"
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
              />
            </div>
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setRejectTarget(null)} className="btn-outline">
                Hủy
              </button>
              <button type="submit" className="btn-primary">
                Xác nhận từ chối
              </button>
            </div>
          </form>
        </div>
      )}
    </AdminLayout>
  );
}
