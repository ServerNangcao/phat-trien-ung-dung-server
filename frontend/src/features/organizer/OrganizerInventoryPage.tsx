import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Clock3, Plus, X } from "lucide-react";
import { formatVnd } from "../../api/platformDemo";
import { ticketingApi } from "../../api/service";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { OrganizerLayout } from "./OrganizerLayout";

const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-[#182226] outline-none focus:border-[#F05B45] focus-visible:ring-2 focus-visible:ring-[#F05B45]/25";

export function OrganizerInventoryPage() {
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const initialEventId = searchParams.get("eventId") ?? "";

  const { data: events = [], isLoading, isError } = useQuery({
    queryKey: ["organizer", "events"],
    queryFn: ticketingApi.organizer.getMyEvents,
  });

  const [selectedEventId, setSelectedEventId] = useState(initialEventId);
  const [showModal, setShowModal] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [tierForm, setTierForm] = useState({
    name: "",
    price: 250000,
    total: 100,
    maxPerOrder: 4,
  });

  useEffect(() => {
    if (initialEventId && events.some((e) => e.id === initialEventId)) {
      setSelectedEventId(initialEventId);
    } else if (!selectedEventId && events.length > 0) {
      setSelectedEventId(events[0].id);
    }
  }, [events, initialEventId, selectedEventId]);

  const currentEvent = events.find((e) => e.id === selectedEventId) ?? events[0];
  const allocatedTotal = currentEvent ? currentEvent.tiers.reduce((sum, t) => sum + t.total, 0) : 0;
  const venueCapacity = currentEvent?.venueCapacity ?? null;
  const hasVenueCapacity = venueCapacity != null && venueCapacity > 0;
  const remainingCapacity = hasVenueCapacity ? venueCapacity - allocatedTotal : null;
  const isOverCapacity = hasVenueCapacity && allocatedTotal > venueCapacity;
  const projectedTotal = allocatedTotal + (Number.isFinite(tierForm.total) ? tierForm.total : 0);
  const wouldExceedCapacity = hasVenueCapacity && projectedTotal > venueCapacity;

  const createTierMutation = useMutation({
    mutationFn: async () => {
      if (!currentEvent) throw new Error("Vui lòng chọn sự kiện.");
      await ticketingApi.organizer.createTicketType(currentEvent.id, {
        name: tierForm.name.trim(),
        price: tierForm.price,
        totalQuantity: tierForm.total,
        maxPerOrder: tierForm.maxPerOrder,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["organizer", "events"] });
      setShowModal(false);
      setFormError(null);
      setTierForm({ name: "", price: 250000, total: 100, maxPerOrder: 4 });
    },
    onError: (err: unknown) => {
      const msg =
        (err as { response?: { data?: { error?: string; detail?: string } }; message?: string })?.response?.data
          ?.error ??
        (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail ??
        (err as { message?: string })?.message ??
        "Không thể thêm hạng vé.";
      setFormError(msg);
    },
  });

  const handleAddTier = (event: FormEvent) => {
    event.preventDefault();
    if (hasVenueCapacity && projectedTotal > venueCapacity) {
      const remaining = Math.max(0, venueCapacity - allocatedTotal);
      setFormError(
        `Tổng số lượng vé phát hành (${projectedTotal.toLocaleString("vi-VN")}) vượt quá sức chứa địa điểm (${venueCapacity.toLocaleString("vi-VN")}). Còn có thể thêm tối đa ${remaining.toLocaleString("vi-VN")} vé.`,
      );
      return;
    }
    setFormError(null);
    createTierMutation.mutate();
  };

  return (
    <OrganizerLayout
      title="Hạng vé & giữ chỗ 10p"
      subtitle="Quản lý số lượng phát hành, vé đã bán và lượng vé đang tạm giữ trong phiên checkout 10 phút."
      action={
        <button
          type="button"
          onClick={() => {
            setFormError(null);
            setShowModal(true);
          }}
          disabled={!currentEvent}
          className="btn-primary disabled:opacity-50"
        >
          <Plus size={16} /> Thêm hạng vé
        </button>
      }
    >
      {isError && (
        <div className="mb-6 rounded-2xl border border-rose-200 bg-rose-50 px-5 py-4 text-sm text-rose-700">
          Không thể tải dữ liệu hạng vé từ máy chủ.
        </div>
      )}

      {isLoading ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-[#526066]">
          Đang tải danh sách sự kiện &amp; hạng vé...
        </div>
      ) : events.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center">
          <p className="text-base font-bold text-[#182226]">Bạn chưa có sự kiện nào</p>
          <p className="mt-1 text-xs text-[#526066]">Hãy tạo sự kiện trước khi thiết lập các hạng vé mở bán.</p>
          <Link to="/organizer/builder" className="btn-primary mt-4 inline-flex">
            Tạo sự kiện mới
          </Link>
        </div>
      ) : (
        <>
          <div className="mb-6 flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-4 text-[#182226]">
            <div className="flex items-center gap-3">
              <label htmlFor="inv-event" className="text-xs font-bold uppercase tracking-wider text-[#526066]">
                Sự kiện:
              </label>
              <select
                id="inv-event"
                value={currentEvent?.id ?? ""}
                onChange={(e) => setSelectedEventId(e.target.value)}
                className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-[#182226]"
              >
                {events.map((ev) => (
                  <option key={ev.id} value={ev.id}>
                    {ev.title}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {currentEvent && (
                <div
                  className={`inline-flex items-center gap-2 rounded-xl border px-3 py-1.5 text-xs font-semibold ${
                    isOverCapacity
                      ? "border-rose-300 bg-rose-50 text-rose-800"
                      : "border-slate-200 bg-slate-50 text-[#182226]"
                  }`}
                >
                  {isOverCapacity && <AlertTriangle size={14} />}
                  <span>
                    Đã phân bổ:{" "}
                    {hasVenueCapacity
                      ? `${allocatedTotal.toLocaleString("vi-VN")} / ${venueCapacity.toLocaleString("vi-VN")} sức chứa`
                      : `${allocatedTotal.toLocaleString("vi-VN")} vé`}
                  </span>
                  {hasVenueCapacity && !isOverCapacity && remainingCapacity !== null && (
                    <span className="text-[#526066]">
                      (còn {remainingCapacity.toLocaleString("vi-VN")} vé)
                    </span>
                  )}
                </div>
              )}
              <div className="inline-flex items-center gap-2 rounded-xl border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-800">
                <Clock3 size={14} />
                <span>Cơ chế giữ chỗ tự động nhả kho sau 10:00 nếu chưa thanh toán</span>
              </div>
            </div>
          </div>

          {currentEvent && isOverCapacity && (
            <div className="mb-6 flex items-center gap-2 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-semibold text-rose-700">
              <AlertTriangle size={16} className="shrink-0" />
              <span>
                Cảnh báo: Tổng số lượng vé phát hành ({allocatedTotal.toLocaleString("vi-VN")}) đang vượt quá sức chứa địa điểm ({venueCapacity?.toLocaleString("vi-VN")}).
              </span>
            </div>
          )}

          {currentEvent && (
            <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white text-[#182226]">
              {currentEvent.tiers.length === 0 ? (
                <div className="p-10 text-center">
                  <p className="text-sm font-bold text-[#182226]">Sự kiện này chưa có hạng vé nào</p>
                  <p className="mt-1 text-xs text-[#526066]">
                    Thêm ít nhất 1 hạng vé để khách hàng có thể đặt chỗ khi sự kiện được duyệt mở bán.
                  </p>
                  <button
                    type="button"
                    onClick={() => {
                      setFormError(null);
                      setShowModal(true);
                    }}
                    className="btn-primary mt-4 inline-flex"
                  >
                    <Plus size={15} /> Thêm hạng vé đầu tiên
                  </button>
                </div>
              ) : (
                <table className="w-full min-w-[680px] text-left text-sm">
                  <thead className="border-b border-slate-200 bg-slate-50 text-xs font-bold uppercase tracking-wider text-[#526066]">
                    <tr>
                      <th className="px-5 py-3.5">Hạng vé</th>
                      <th className="px-5 py-3.5">Giá vé</th>
                      <th className="px-5 py-3.5">Tổng phát hành</th>
                      <th className="px-5 py-3.5">Đã bán</th>
                      <th className="px-5 py-3.5">Đang giữ (10p)</th>
                      <th className="px-5 py-3.5">Còn lại</th>
                      <th className="px-5 py-3.5">Tối đa/đơn</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 bg-white">
                    {currentEvent.tiers.map((tier) => {
                      const available =
                        tier.available ?? Math.max(0, tier.total - tier.sold - tier.reserved);
                      return (
                        <tr key={tier.id} className="hover:bg-slate-50">
                          <td className="px-5 py-4">
                            <p className="font-bold text-[#182226]">{tier.name}</p>
                            <p className="text-xs text-[#526066]">{tier.description}</p>
                          </td>
                          <td className="px-5 py-4 font-bold text-[#182226]">{formatVnd(tier.price)}</td>
                          <td className="px-5 py-4">{tier.total}</td>
                          <td className="px-5 py-4 font-semibold text-emerald-700">{tier.sold}</td>
                          <td className="px-5 py-4">
                            <StatusBadge tone={tier.reserved > 0 ? "warning" : "neutral"}>
                              {tier.reserved} vé
                            </StatusBadge>
                          </td>
                          <td className="px-5 py-4 font-bold text-[#182226]">{available}</td>
                          <td className="px-5 py-4 text-[#526066]">{tier.maxPerOrder}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </div>
          )}
        </>
      )}

      {showModal && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="add-tier-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"
        >
          <form
            onSubmit={handleAddTier}
            className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 text-[#182226] shadow-2xl space-y-4"
          >
            <div className="flex items-center justify-between">
              <h2 id="add-tier-title" className="text-lg font-extrabold text-[#182226]">
                Thêm hạng vé mới
              </h2>
              <button
                type="button"
                aria-label="Đóng hộp thoại"
                onClick={() => setShowModal(false)}
                className="rounded-lg p-1.5 text-[#526066] hover:bg-black/5 cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>

            {hasVenueCapacity && (
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-[#526066]">
                Sức chứa địa điểm:{" "}
                <span className="font-bold text-[#182226]">{venueCapacity.toLocaleString("vi-VN")}</span> · Đã phân bổ:{" "}
                <span className="font-bold text-[#182226]">{allocatedTotal.toLocaleString("vi-VN")}</span> · Còn lại:{" "}
                <span className="font-bold text-emerald-700">
                  {Math.max(0, remainingCapacity ?? 0).toLocaleString("vi-VN")} vé
                </span>
              </div>
            )}

            {formError && (
              <p className="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-xs text-rose-700">
                {formError}
              </p>
            )}

            {wouldExceedCapacity && (
              <div className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                <AlertTriangle size={15} className="mt-0.5 shrink-0 text-amber-700" />
                <span>
                  Tổng vé sau khi thêm ({projectedTotal.toLocaleString("vi-VN")}) sẽ vượt quá sức chứa địa điểm (
                  {venueCapacity?.toLocaleString("vi-VN")}).
                </span>
              </div>
            )}

            <div>
              <label htmlFor="tier-name" className="block text-xs font-semibold text-[#526066] mb-1">
                Tên hạng vé
              </label>
              <input
                id="tier-name"
                className={inputClass}
                placeholder="VD: Early Bird, Standard, VIP..."
                value={tierForm.name}
                onChange={(e) => setTierForm({ ...tierForm, name: e.target.value })}
                required
              />
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <label htmlFor="tier-price" className="block text-xs font-semibold text-[#526066] mb-1">
                  Giá (VNĐ)
                </label>
                <input
                  id="tier-price"
                  type="number"
                  min={0}
                  step={10000}
                  className={inputClass}
                  value={tierForm.price}
                  onChange={(e) => setTierForm({ ...tierForm, price: Number(e.target.value) })}
                  required
                />
              </div>
              <div>
                <label htmlFor="tier-total" className="block text-xs font-semibold text-[#526066] mb-1">
                  Số lượng
                </label>
                <input
                  id="tier-total"
                  type="number"
                  min={1}
                  className={inputClass}
                  value={tierForm.total}
                  onChange={(e) => setTierForm({ ...tierForm, total: Number(e.target.value) })}
                  required
                />
              </div>
              <div>
                <label htmlFor="tier-max" className="block text-xs font-semibold text-[#526066] mb-1">
                  Tối đa/đơn
                </label>
                <input
                  id="tier-max"
                  type="number"
                  min={1}
                  max={20}
                  className={inputClass}
                  value={tierForm.maxPerOrder}
                  onChange={(e) => setTierForm({ ...tierForm, maxPerOrder: Number(e.target.value) })}
                  required
                />
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <button type="button" onClick={() => setShowModal(false)} className="btn-outline">
                Hủy
              </button>
              <button type="submit" disabled={createTierMutation.isPending} className="btn-primary">
                {createTierMutation.isPending ? "Đang lưu..." : "Lưu hạng vé"}
              </button>
            </div>
          </form>
        </div>
      )}
    </OrganizerLayout>
  );
}
